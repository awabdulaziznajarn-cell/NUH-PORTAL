using MapsterMapper;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Bulk;
using NUH_PORTAL.DTOs.Common;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;
using System.Data;
using System.Text.RegularExpressions;

namespace NUH_PORTAL.Services
{
    // التسجيل الجماعي - اتنقل من BulkRegistrationController (بنفس المنطق والمعاملة transaction)
    public class BulkRegistrationService : AppServiceBase, IBulkRegistrationService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Request> _requests;
        private readonly IRepository<BulkRequest> _bulkRequests;
        private readonly IRepository<BulkRequestStudent> _bulkStudents;
        private readonly IRepository<Notification> _notifications;
        private readonly IRepository<AuditLog> _auditLogs;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<BulkRegistrationService> _logger;
        private readonly ILookupResolver _lookups;
        // ⚠️ الإكسل أخطر مسار في موضوع السعة: شيت واحد ممكن يحطّ عشرة في غرفة
        //    واحدة، وكلهم لسه مش في الجدول - فالعدّ من قاعدة البيانات وحده كان
        //    هيقول إنها فاضية عشر مرات.
        private readonly IHousingCapacityGuard _capacity;

        public BulkRegistrationService(
            IRepository<Student> students,
            IRepository<Request> requests,
            IRepository<BulkRequest> bulkRequests,
            IRepository<BulkRequestStudent> bulkStudents,
            IRepository<Notification> notifications,
            IRepository<AuditLog> auditLogs,
            IHttpContextAccessor http,
            ILogger<BulkRegistrationService> logger,
            ILookupResolver lookups,
            IHousingCapacityGuard capacity,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _requests = requests;
            _bulkRequests = bulkRequests;
            _bulkStudents = bulkStudents;
            _notifications = notifications;
            _auditLogs = auditLogs;
            _http = http;
            _logger = logger;
            _lookups = lookups;
            _capacity = capacity;
        }

        public FileResultDto GetTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Template");
            // ⚠️ FloorNumber كان ناقص من القالب رغم إن السكن عندنا مبنى/دور/شقة/غرفة،
            //    فكل طالب بيترفع بالإكسل كان بيتسجّل بدور فاضي.
            var headers = new[] { "StudentID", "NationalID", "FullNameArabic", "FullNameEnglish", "Mobile", "College", "Department", "AcademicLevel", "Gender", "BuildingNumber", "FloorNumber", "ApartmentNumber", "RoomNumber" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return new FileResultDto
            {
                Content = stream.ToArray(),
                FileName = "BulkRegistrationTemplate.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        public async Task<BulkValidationResultDto> ValidateFileAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                throw new UserFriendlyException("الرجاء رفع ملف", 400);
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException("يُسمح فقط بملفات Excel (.xlsx)", 400);
            if (file.Length > 10 * 1024 * 1024)
                throw new UserFriendlyException("حجم الملف يجب أن لا يتجاوز 10 ميجابايت", 400);

            DataTable dt;
            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheet(1);
                dt = new DataTable();
                var firstRow = ws.FirstRowUsed()?.CellsUsed().ToList() ?? new();
                foreach (var cell in firstRow)
                    dt.Columns.Add(cell.Value.ToString().Trim());

                var rows = ws.RangeUsed()?.RowsUsed().Skip(1).ToList() ?? new();
                foreach (var row in rows)
                {
                    var dr = dt.NewRow();
                    var cells = row.Cells().ToList();
                    for (int i = 0; i < dt.Columns.Count && i < cells.Count; i++)
                        dr[i] = cells[i].Value.ToString().Trim();
                    dt.Rows.Add(dr);
                }
            }
            catch
            {
                throw new UserFriendlyException("فشل قراءة الملف. تأكد من صيغة الملف.", 400);
            }

            // ⚠️ السقف قبل أي معالجة: الملف محدود بعشرة ميجا، لكن xlsx عالي
            //    الضغط بيتمدّد لملايين الصفوف - وقراءتها كلها في DataTable
            //    بتاكل الذاكرة قبل ما يوصل أول فحص. الرفض هنا بيبقى فوري.
            if (dt.Rows.Count > MaxRows)
                throw new UserFriendlyException(TooManyRowsError, 400);

            var errors = new List<RowErrorDto>();
            var preview = new List<PreviewDto>();
            int validCount = 0, errorCount = 0;
            var processedStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // الأماكن المحجوزة داخل الشيت نفسه: مفتاحها مبنى/دور/شقة/غرفة،
            // وبتتزوّد مع كل صفّ سليم فالصفّ اللي بعده بيشوف المكان مشغول.
            var sheetRooms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // قسم الموظف اللي بيرفع الملف - بيملّي خانة الجنس الفاضية، وبيرفض
            // الصف اللي جنسه من القسم التاني. القاعدة في Core/GenderScope.cs.
            var scope = UnitOfWork.GetGenderScope();
            var scopeLabel = scope == Gender.Male ? "الطلاب" : "الطالبات";

            var existingStudentIds = new HashSet<string>(
                await _students.Query().AsNoTracking()
                    .Where(s => s.student_id != null)
                    .Select(s => s.student_id!)
                    .ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                var row = dt.Rows[i];
                var studentId = GetCol(dt, row, "StudentID");
                var nationalId = GetCol(dt, row, "NationalID");
                var fullNameAr = GetCol(dt, row, "FullNameArabic");
                var fullNameEn = GetCol(dt, row, "FullNameEnglish");
                var mobile = GetCol(dt, row, "Mobile");
                var gender = GetCol(dt, row, "Gender");
                var buildingNumber = GetCol(dt, row, "BuildingNumber");
                var floorNumber = GetCol(dt, row, "FloorNumber");
                var apartmentNumber = GetCol(dt, row, "ApartmentNumber");
                var roomNumber = GetCol(dt, row, "RoomNumber");
                var academicLevel = GetCol(dt, row, "AcademicLevel");
                var college = GetCol(dt, row, "College");
                var department = GetCol(dt, row, "Department");

                bool rowHasError = false;
                void AddError(string column, string description)
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = column, ErrorDescription = description });
                }

                if (string.IsNullOrEmpty(studentId))
                    AddError("StudentID", "الرقم الجامعي مطلوب - " + IdentityRules.StudentIdError);
                // ⚠️ كان ^\d{9,10}$ بينما شدّدت StudentService القاعدة إلى تسعة
                //    أرقام تبدأ بـ 4. القاعدة الآن واحدة في Core/IdentityRules.cs.
                else if (!IdentityRules.IsValidStudentId(studentId))
                    AddError("StudentID", IdentityRules.StudentIdError);
                else if (existingStudentIds.Contains(studentId))
                    AddError("StudentID", "الرقم الجامعي موجود مسبقاً في النظام - لا يمكن تكرار الرقم الجامعي");
                else if (processedStudentIds.Contains(studentId))
                    AddError("StudentID", "الرقم الجامعي مكرر في الملف - تم إدخال نفس الرقم الجامعي في أكثر من سطر");

                if (string.IsNullOrEmpty(nationalId))
                    AddError("NationalID", "رقم الهوية الوطنية مطلوب - " + IdentityRules.NationalIdError);
                // ⚠️ كانت أي عشرة أرقام - نفس ضعف StudentService. القاعدة الموحّدة
                //    بتمنع رقم الجوال يتحفظ في خانة الهوية من رفع الإكسل كمان.
                else if (!IdentityRules.IsValidNationalId(nationalId))
                    AddError("NationalID", IdentityRules.NationalIdError);

                if (string.IsNullOrEmpty(fullNameAr))
                    AddError("FullNameArabic", "الاسم العربي مطلوب - يجب إدخال الاسم باللغة العربية");
                else if (!Regex.IsMatch(fullNameAr, ArabicNamePattern))
                    AddError("FullNameArabic", "الاسم العربي يحتوي على أحرف غير عربية - يجب إدخال الاسم باللغة العربية فقط");

                if (string.IsNullOrEmpty(fullNameEn))
                    AddError("FullNameEnglish", "الاسم الإنجليزي مطلوب - يجب إدخال الاسم باللغة الإنجليزية");
                else if (!Regex.IsMatch(fullNameEn, EnglishNamePattern))
                    AddError("FullNameEnglish", "الاسم الإنجليزي يحتوي على أحرف غير إنجليزية - يجب إدخال الاسم باللغة الإنجليزية فقط");

                if (string.IsNullOrEmpty(mobile))
                    AddError("Mobile", "رقم الجوال مطلوب - يجب إدخال رقم جوال صحيح");
                else if (!Regex.IsMatch(mobile, IdentityRules.StoredMobilePattern))
                    AddError("Mobile", IdentityRules.MobileError);

                // ⚠️ كان اختياريًا. الطالب اللي بيتحمّل بلا جنس مايوصلش لا لمشرف
                //    قسم الطلاب ولا لمشرفة قسم الطالبات - طلبه بيقع في فراغ ومحدش
                //    شايفه. الجنس بقى هو اللي بيوجّه الطلب، فبقى إجباريًا.
                //
                //  ⚠️ لكن مشرف القسم بيرفع طلاب قسمه هو - فالخانة الفاضية
                //     بتتملي من قسمه بدل ما نرفض الصف ونطلب منه يكتب نفس الكلمة
                //     في كل سطر. أما لو كتب القسم التاني بإيده فالصف بيترفض ولا
                //     بنقلب القيمة: الاحتمال الأكبر إنه غلط في السطر نفسه (لصق
                //     بيانات طالب من كشف تاني)، وقلبها بيبني سجل باسم واحد وجنس
                //     واحد تاني. الأدمن (نطاقه القسمين) ما بيتغيّرش عليه حاجة.
                if (string.IsNullOrEmpty(gender) && scope != null)
                    gender = GenderHelper.ToStr(scope)!;

                if (string.IsNullOrEmpty(gender))
                    AddError("Gender", "الجنس مطلوب - يجب إدخال: ذكر أو أنثى (Male / Female). الطلب بيتوجّه لمشرف القسم بناءً عليه");
                else if (!GenderHelper.IsValid(gender))
                    AddError("Gender", "قيمة الجنس غير صحيحة - القيم المسموح بها: ذكر, أنثى, Male, Female");
                else if (!GenderScope.CanWrite(scope, GenderHelper.Parse(gender)))
                    AddError("Gender", $"هذا السطر خارج نطاق قسمك - أنت مسؤول عن قسم {scopeLabel} فقط. صحّح قيمة الجنس أو احذف السطر من الملف");

                if (!string.IsNullOrEmpty(academicLevel) && !Regex.IsMatch(academicLevel, @"^[1-5]$"))
                    AddError("AcademicLevel", "المستوى الدراسي غير صحيح - يجب أن يكون رقماً بين 1 و 5");

                // ⚠️ أسلوب ترقيم المبنى بيتجاب مرة واحدة للصفّ: فحوص الدور والشقة
                //    والغرفة تحت كلها بتتوقف عليه، و«شقة ٢ في الدور ٣» صحيحة في
                //    سكن الطالبات وغلط في سكن الطلاب.
                var rowScheme = await _lookups.NumberingForBuildingAsync(buildingNumber);

                if (string.IsNullOrEmpty(buildingNumber))
                    AddError("BuildingNumber", "رقم المبنى السكني مطلوب");
                else
                {
                    // ⚠️ كان Regex ‎^(4[0-3]|6[5-9]|70)$‎ مكتوب هنا، ونسخة تانية
                    //    (HashSet) في StudentService - والمباني جدول مُدار.
                    //    فالمدير يضيف مبنى ٧١ ويلاقيه مرفوض من رفع الإكسل.
                    //    المصدر الوحيد دلوقتي في ILookupResolver.
                    var bErr = await _lookups.BuildingCodeErrorAsync(buildingNumber);
                    if (bErr != null) AddError("BuildingNumber", bErr);
                }

                if (string.IsNullOrEmpty(floorNumber))
                    AddError("FloorNumber", "رقم الدور مطلوب - القيم المسموح بها: 0 (الأرضي) حتى 4");
                else if (!Regex.IsMatch(floorNumber, @"^[0-4]$"))
                    AddError("FloorNumber", "رقم الدور غير صحيح - القيم المسموح بها: 0 (الأرضي), 1, 2, 3, 4");

                if (string.IsNullOrEmpty(apartmentNumber))
                    AddError("ApartmentNumber", "رقم الشقة مطلوب");
                else if (!Regex.IsMatch(apartmentNumber, @"^\d+$"))
                    AddError("ApartmentNumber", "رقم الشقة غير صحيح - يجب أن يكون رقماً فقط");
                // القاعدة من Core/HousingStructure - نفس المصدر اللي بتقرا منه الشاشة.
                // من غير الفحص ده الشيت ممكن يسكّن طالب في شقة مش موجودة في دوره.
                else if (!HousingStructure.ApartmentBelongsToFloor(rowScheme, floorNumber, apartmentNumber))
                {
                    int.TryParse(floorNumber, out var rowFloor);
                    AddError("ApartmentNumber", $"رقم الشقة {apartmentNumber} لا ينتمي للدور {floorNumber} - الشقق المسموح بها في هذا الدور: {string.Join("، ", HousingStructure.ApartmentsFor(rowScheme, rowFloor))}");
                }

                if (string.IsNullOrEmpty(roomNumber))
                    AddError("RoomNumber", "رقم الغرفة مطلوب");
                else if (!Regex.IsMatch(roomNumber, @"^\d+$"))
                    AddError("RoomNumber", "رقم الغرفة غير صحيح - يجب أن يكون رقماً فقط");
                // ⚠️ والغرفة لازم تكون **من غرف الشقة**: في سكن الطلاب الترقيم متّصل
                //    عبر المبنى (شقة ٥ غرفها ١٧-٢٠)، فغرفة ٣ في شقة ٥ رقم مالوش وجود.
                //    الفحص ده مكانش موجود خالص - الشيت كان بيعدّي بأي رقم غرفة.
                else if (int.TryParse(apartmentNumber, out var rowApt)
                         && int.TryParse(roomNumber, out var rowRoom)
                         && !HousingStructure.IsValidRoom(rowScheme, rowApt, rowRoom))
                    AddError("RoomNumber", $"رقم الغرفة {roomNumber} لا ينتمي للشقة {apartmentNumber} - الغرف المسموح بها: {string.Join("، ", HousingStructure.RoomsFor(rowScheme, rowApt))}");

                // ⚠️ بعد فحوص البنية عشان مانحسبش مكانًا لصفّ أرقامه غلط أصلًا.
                //    والحدّ الأقصى مسموح: الرفع بيعمله موظف الإسكان.
                if (!rowHasError)
                {
                    var roomKey = $"{buildingNumber}|{floorNumber}|{apartmentNumber}|{roomNumber}";
                    sheetRooms.TryGetValue(roomKey, out var pendingHere);

                    var capacityError = await _capacity.CheckAsync(
                        buildingNumber, floorNumber, apartmentNumber, roomNumber,
                        excludeStudentId: null, allowExceptionSlot: true, pendingInSameRoom: pendingHere);

                    if (capacityError != null) AddError("RoomNumber", capacityError);
                    else sheetRooms[roomKey] = pendingHere + 1;
                }

                processedStudentIds.Add(studentId);

                if (rowHasError)
                {
                    errorCount++;
                }
                else
                {
                    validCount++;
                    preview.Add(new PreviewDto
                    {
                        StudentID = studentId,
                        NationalID = nationalId,
                        FullNameArabic = fullNameAr,
                        FullNameEnglish = fullNameEn,
                        Mobile = mobile,
                        College = college,
                        Department = department,
                        AcademicLevel = academicLevel,
                        Gender = gender,
                        BuildingNumber = buildingNumber,
                        FloorNumber = floorNumber,
                        ApartmentNumber = apartmentNumber,
                        RoomNumber = roomNumber
                    });
                }
            }

            return new BulkValidationResultDto
            {
                TotalRecords = dt.Rows.Count,
                ValidRecords = validCount,
                ErrorRecords = errorCount,
                Errors = errors,
                Preview = preview,
                FileName = file.FileName
            };
        }

        // ====================================================================
        //  قواعد صيغة الصفّ - تعريف واحد يستعمله التحقّق والإنشاء.
        //
        //  ⚠️ الفحوص دي كانت في ValidateFileAsync **وبس**، وهي نداء HTTP
        //     منفصل عن الإنشاء. يعني اللي بيبعت على /create مباشرة كان بيتخطّاها
        //     كلها: رقم جامعي بأي شكل، اسم بأي حروف، جوال بأي صيغة - وبيتكتبوا
        //     في قاعدة البيانات زي ما جم.
        //
        //  ⚠️ وde مش خطر نظري: الرقم الجامعي بيروح بعدين لبناء اسم الكائن في
        //     الدليل النشط (ADProvisioningService: CN=h{الرقم},{المسار}) بلا
        //     هروب، فرقم فيه فاصلة أو باك سلاش بيفسد الـ DN أو يفشل الإنشاء.
        //
        //  ⚠️ والقاعدة الأصل: **الفحص على الباب اللي بيكتب**، لا على الباب
        //     اللي بيسبقه. التحقّق شغلته يوري الموظف الغلط قبل ما يضغط حفظ؛
        //     أما اللي بيحمي قاعدة البيانات فهو اللي هنا.
        // ====================================================================
        private const string ArabicNamePattern  = @"^[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\s]+$";
        private const string EnglishNamePattern = @"^[a-zA-Z\s]+$";

        // ⚠️ سقف الصفوف: الملف محدود بعشرة ميجا، لكن ملف xlsx عالي الضغط
        //    بيتمدّد لملايين الصفوف - وكلها بتتقرا في الذاكرة وبتتكتب في معاملة
        //    واحدة. السقف بيخلّي الرفض سريعًا وواضحًا بدل ما السيرفر يقع.
        public const int MaxRows = 2000;

        // ⚠️ الرسالة مبنية من MaxRows لا مكتوب فيها الرقم: رقم متكرّر في نصّ
        //    بيفضل مكانه لما القيمة تتغيّر، والموظف بيقرا حدًّا غير الحدّ الفعلي.
        public static readonly string TooManyRowsError =
            $"الملف يحتوي على صفوف أكثر من الحد المسموح ({MaxRows} صف). قسّمه على أكثر من ملف.";

        // بترجّع أول مخالفة صيغة في الصفّ، أو null لو سليم.
        private static string? RowFormatError(BulkStudentDto s)
        {
            var studentId  = (s.StudentID ?? "").Trim();
            var nationalId = (s.NationalID ?? "").Trim();
            var nameAr     = (s.FullNameArabic ?? "").Trim();
            var nameEn     = (s.FullNameEnglish ?? "").Trim();
            var mobile     = (s.Mobile ?? "").Trim();

            if (!IdentityRules.IsValidStudentId(studentId))   return IdentityRules.StudentIdError;
            if (!IdentityRules.IsValidNationalId(nationalId)) return IdentityRules.NationalIdError;
            if (!Regex.IsMatch(nameAr, ArabicNamePattern))    return "الاسم العربي غير صالح - يجب إدخال الاسم باللغة العربية فقط";
            if (!Regex.IsMatch(nameEn, EnglishNamePattern))   return "الاسم الإنجليزي غير صالح - يجب إدخال الاسم باللغة الإنجليزية فقط";
            if (!Regex.IsMatch(mobile, IdentityRules.StoredMobilePattern)) return IdentityRules.MobileError;

            return null;
        }

        public async Task<BulkCreateResultDto> CreateAsync(BulkCreateDto dto)
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId <= 0)
                throw new UserFriendlyException("غير مصرح", 401);

            if (dto.Students == null || dto.Students.Count == 0)
                throw new UserFriendlyException("لا يوجد طلاب صالحون للتسجيل", 400);

            if (dto.Students.Count > MaxRows)
                throw new UserFriendlyException(TooManyRowsError, 400);

            // ⚠️ فحص الصيغة هنا لا في التحقّق وحده: التحقّق والإنشاء نداءان
            //    منفصلان، والعميل هو اللي بيبعت القائمة تاني - فاللي اتفحص مش
            //    بالضرورة اللي وصل. الشرح الكامل عند RowFormatError.
            var badFormat = dto.Students
                .Select((s, i) => new { Row = i + 1, s.StudentID, Error = RowFormatError(s) })
                .Where(x => x.Error != null)
                .Take(10)
                .Select(x => $"سطر {x.Row} ({x.StudentID}): {x.Error}")
                .ToList();
            if (badFormat.Count > 0)
                throw new UserFriendlyException(
                    "بيانات غير صالحة: " + string.Join(" | ", badFormat), 400);

            // اللي بيراجع مرحلة الإسكان لما يرفع الملف بنفسه → موافقة الإسكان تلقائيًا
            var isHousingCreator = UnitOfWork.HasPermission("requests.reviewHousing");
            // الدور بيتخزّن في الطلب كبيانات (مين قدّمه) - مش فحص صلاحية
            var role = UnitOfWork.GetCurrentUserRole()?.ToLower();

            using var transaction = await UnitOfWork.BeginTransactionAsync();
            try
            {
                // منع التكرار داخل نفس الملف
                var dupInBatch = dto.Students
                    .Where(s => !string.IsNullOrEmpty(s.StudentID))
                    .GroupBy(s => s.StudentID, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (dupInBatch.Count > 0)
                    throw new UserFriendlyException($"بيانات مكررة في نفس الملف: {string.Join(", ", dupInBatch)}", 409);

                // ⚠️ التحقق (validate) والإنشاء (create) نداءين HTTP منفصلين،
                //    والعميل هو اللي بيبعت القائمة تاني في التاني - يعني اللي
                //    فحصناه في الأول مش بالضرورة اللي وصل هنا. فالفحص ده مش
                //    تكرار للتحقق، ده الحارس الحقيقي؛ والتحقق شغلته إنه يوري
                //    الموظف الغلط قبل ما يضغط حفظ.
                var scope = UnitOfWork.GetGenderScope();
                foreach (var s in dto.Students)
                    s.Gender = GenderHelper.ToStr(GenderScope.Resolve(scope, GenderHelper.Parse(s.Gender))) ?? s.Gender;

                var badGender = dto.Students
                    .Where(s => !GenderScope.CanWrite(scope, GenderHelper.Parse(s.Gender))
                             || GenderHelper.Parse(s.Gender) == null)
                    .Select(s => s.StudentID)
                    .ToList();
                if (badGender.Count > 0)
                    throw new UserFriendlyException($"طلاب خارج نطاق قسمك أو بلا جنس محدد: {string.Join(", ", badGender)}", 403);

                // منع تسجيل طلاب موجودين مسبقًا
                var newIdsHash = dto.Students.Where(s => !string.IsNullOrEmpty(s.StudentID)).Select(s => s.StudentID).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingIds = new HashSet<string>(await _students.Query().AsNoTracking()
                    .Where(s => s.student_id != null && newIdsHash.Contains(s.student_id))
                    .Select(s => s.student_id!)
                    .ToListAsync(), StringComparer.OrdinalIgnoreCase);
                if (existingIds.Count > 0)
                    throw new UserFriendlyException($"بعض الطلاب مسجلين مسبقاً: {string.Join(", ", existingIds)}", 409);

                var bulkRequest = new BulkRequest
                {
                    FileName = dto.FileName,
                    RecordCount = dto.Students.Count,
                    ValidCount = dto.Students.Count,
                    ErrorCount = 0,
                    CreatedBy = actorId,
                    CreatedDate = DateTime.UtcNow,
                    Status = "pending"
                };
                await _bulkRequests.AddAsync(bulkRequest);
                await UnitOfWork.SaveAsync();

                bulkRequest.RequestNumber = $"BRQ-{bulkRequest.Id:D6}";

                var bulkStudents = dto.Students.Select(s => new BulkRequestStudent
                {
                    BulkRequestId = bulkRequest.Id,
                    StudentID = s.StudentID,
                    NationalID = s.NationalID,
                    FullNameArabic = s.FullNameArabic,
                    FullNameEnglish = s.FullNameEnglish,
                    Mobile = s.Mobile,
                    College = s.College,
                    Department = s.Department,
                    AcademicLevel = s.AcademicLevel,
                    Gender = s.Gender,
                    BuildingNumber = s.BuildingNumber,
                    FloorNumber = s.FloorNumber,
                    ApartmentNumber = s.ApartmentNumber,
                    RoomNumber = s.RoomNumber,
                    IsValid = true
                }).ToList();

                await _bulkStudents.AddRangeAsync(bulkStudents);
                await UnitOfWork.SaveAsync();

                // إنشاء سجلات الطلاب
                var students = dto.Students.Select(s => new Student
                {
                    student_id = s.StudentID,
                    national_id = s.NationalID,
                    full_name = s.FullNameArabic,
                    full_name_english = s.FullNameEnglish,
                    phone = s.Mobile,
                    college = s.College,
                    department = s.Department,
                    academic_level = s.AcademicLevel,
                    gender = GenderHelper.Parse(s.Gender),
                    housing_building = s.BuildingNumber,
                    floor_number = s.FloorNumber,
                    apartment_number = s.ApartmentNumber,
                    room_number = s.RoomNumber,
                    created_at = DateTime.UtcNow,
                    created_by = actorId,
                    status = StudentState.active,
                    student_status = StudentStatus.active
                }).ToList();

                await _lookups.ApplyAsync(students); // FK ids من الأكواد (dual-write)
                await _students.AddRangeAsync(students);
                await UnitOfWork.SaveAsync();

                // إنشاء طلب لكل طالب
                var defaultStatus = isHousingCreator ? "cyber_review" : "submitted";
                var requests = students.Select(st => new Request
                {
                    RequestType = RequestType.bulk_req,
                    StudentId = st.Id,
                    // منه بيتحدد المشرف المسؤول عن الطلب
                    StudentGender = st.gender,
                    SubmittedBy = actorId,
                    SubmittedAt = DateTime.UtcNow,
                    Status = defaultStatus,
                    RequestedByRole = role,
                    BulkRequestId = bulkRequest.Id
                }).ToList();

                await _requests.AddRangeAsync(requests);
                await UnitOfWork.SaveAsync();

                // إشعارات + سجلات audit (دفعة واحدة وحفظة واحدة - مش حفظة لكل سجل)
                var ctx = _http.HttpContext;
                var ip = ctx?.Connection.RemoteIpAddress?.ToString();
                var ua = ctx?.Request.Headers["User-Agent"].ToString();
                var notifRoles = isHousingCreator ? new[] { "cyber" } : new[] { "admin", "supervisor", "cyber" };

                foreach (var req in requests)
                {
                    var reqNum = $"BRQ-{bulkRequest.Id:D6}-S{req.StudentId}";
                    var notifMsg = isHousingCreator
                        ? $"تم تقديم طلب جماعي جديد وإحالته للمراجعة الإلكترونية ({reqNum})"
                        : $"تم تقديم طلب جماعي جديد ({reqNum})";
                    foreach (var nr in notifRoles)
                    {
                        await _notifications.AddAsync(new Notification
                        {
                            request_id = req.Id,
                            channel = "in_app",
                            recipient_role = nr,
                            message = notifMsg,
                            status = NotificationStatus.pending,
                            sent_at = DateTime.UtcNow
                        });
                    }

                    await _auditLogs.AddAsync(new AuditLog
                    {
                        user_id = actorId,
                        action = "bulk_registration_created",
                        target_table = "Requests",
                        target_id = req.Id,
                        action_at = DateTime.UtcNow,
                        ip_address = ip,
                        user_agent = ua
                    });
                }

                await UnitOfWork.SaveAsync();
                await transaction.CommitAsync();

                return new BulkCreateResultDto
                {
                    Id = bulkRequest.Id,
                    RequestId = requests.First().Id,
                    RequestNumber = bulkRequest.RequestNumber,
                    RecordCount = bulkRequest.RecordCount
                };
            }
            catch (UserFriendlyException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); }
                catch (Exception rollbackEx) { _logger.LogError(rollbackEx, "BulkRegistration/create rollback error"); }

                var errorRef = Guid.NewGuid().ToString("N")[..12];
                _logger.LogError(ex, "BulkRegistration/create error [Ref={Ref}]", errorRef);

                var detail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new UserFriendlyException($"حدث خطأ أثناء إنشاء الطلب الجماعي (Ref: {errorRef}): {detail}", 500);
            }
        }

        public async Task<List<BulkRequestDto>> GetAllAsync()
        {
            var list = await _bulkRequests.Query().AsNoTracking()
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();
            return Mapper.Map<List<BulkRequestDto>>(list);
        }

        public async Task<BulkRequestDetailsDto> GetByIdAsync(int id)
        {
            var req = await _bulkRequests.Query().AsNoTracking()
                .Include(b => b.Students)
                .FirstOrDefaultAsync(b => b.Id == id)
                ?? throw UserFriendlyException.NotFound("الطلب الجماعي غير موجود");

            return Mapper.Map<BulkRequestDetailsDto>(req);
        }

        // ----------------------------- Helpers -----------------------------

        // قراءة عمود لو موجود (الكود القديم كان بيرمي استثناء لو العمود ناقص من الملف)
        private static string GetCol(DataTable dt, DataRow row, string name)
            => dt.Columns.Contains(name) ? row[name]?.ToString()?.Trim() ?? "" : "";
    }
}
