using MapsterMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Attachments;
using NUH_PORTAL.DTOs.Housing;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // نقل سكن الطلاب بواسطة المشرف — اتنقل من SupervisorHousingTransferController
    public class SupervisorHousingTransferService : AppServiceBase, ISupervisorHousingTransferService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<HousingTransfer> _transfers;
        // ⚠️ عشان أسلوب ترقيم المبنى: «شقة ٢ في الدور ٣» صحيحة في سكن
        //    الطالبات وغلط في سكن الطلاب - والفحص لازم يعرف الاتنين.
        private readonly ILookupResolver _lookups;
        // ⚠️ الحارس مش شرط زيادة: النقل كان بيقدر يحطّ رابع في غرفة سعتها
        //    تلاتة، والخريطة تفضل تورّي «تجاوزت السعة» بعد وقوعها.
        private readonly IHousingCapacityGuard _capacity;
        private readonly IRepository<AccountLifecycleLog> _lifecycle;
        private readonly IAuditService _audit;
        private readonly IAttachmentStorage _storage;
        private readonly IHttpContextAccessor _http;

        // ⚠️ القائمة وحدّ الحجم وفحص البصمة في Core/AttachmentPolicy - مصدر واحد
        //    مشترك مع StudentStatusService بدل نسختين تتفاوتان.
        public const long MaxFileSize = AttachmentPolicy.MaxFileSize;

        public SupervisorHousingTransferService(
            IRepository<Student> students,
            IRepository<HousingTransfer> transfers,
            ILookupResolver lookups,
            IHousingCapacityGuard capacity,
            IRepository<AccountLifecycleLog> lifecycle,
            IAuditService audit,
            IAttachmentStorage storage,
            IHttpContextAccessor http,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _transfers = transfers;
            _lookups = lookups;
            _capacity = capacity;
            _lifecycle = lifecycle;
            _audit = audit;
            _storage = storage;
            _http = http;
        }

        public async Task<TransferResultDto> CreateTransferAsync(string studentNumber, string newBuilding, string newFloor, string newApartment, string newRoom, string reason, string? customReason, IFormFile? file)
        {
            // النقل بصلاحية housing.transfer — كان مقفول على دورين بالاسم، فالأدمن
            // كان بيلاقي الفورم بيرفض دايمًا وأي دور جديد كمان.
            // سجل التنقلات بيسجّل مين نفّذ، فالمساءلة محفوظة.
            if (!UnitOfWork.HasPermission("housing.transfer"))
                throw UserFriendlyException.Forbidden();

            if (string.IsNullOrEmpty(studentNumber))
                throw new UserFriendlyException("الرقم الجامعي مطلوب", 400);
            if (string.IsNullOrEmpty(newBuilding))
                throw new UserFriendlyException("رقم المبنى الجديد مطلوب", 400);
            if (string.IsNullOrEmpty(newFloor))
                throw new UserFriendlyException("رقم الدور الجديد مطلوب", 400);
            if (string.IsNullOrEmpty(newApartment))
                throw new UserFriendlyException("رقم الشقة الجديدة مطلوب", 400);

            // الشقة لازم تكون ضمن شقق الدور المختار — التحقق هنا مش في الشاشة بس،
            // عشان أي نداء مباشر للـ API مايقدرش يسكّن طالب في شقة مش في دوره.
            var scheme = await _lookups.NumberingForBuildingAsync(newBuilding);
            if (!HousingStructure.ApartmentBelongsToFloor(scheme, newFloor, newApartment))
                throw new UserFriendlyException("رقم الشقة لا ينتمي للدور المختار", 400);
            if (string.IsNullOrEmpty(newRoom))
                throw new UserFriendlyException("رقم الغرفة الجديدة مطلوب", 400);
            // ⚠️ والغرفة لازم تكون **من غرف الشقة**: في سكن الطلاب رقم الغرفة
            //    متّصل عبر المبنى (شقة ٥ غرفها ١٧-٢٠)، فغرفة ٣ في شقة ٥ رقم
            //    مالوش وجود. الشاشة بتمنعه والخادم مكانش بيشوفه.
            if (!int.TryParse(newApartment?.Trim(), out var __apt)
                || !int.TryParse(newRoom.Trim(), out var __room)
                || !HousingStructure.IsValidRoom(scheme, __apt, __room))
                throw new UserFriendlyException("رقم الغرفة لا ينتمي للشقة المختارة", 400);
            if (string.IsNullOrEmpty(reason))
                throw new UserFriendlyException("سبب النقل مطلوب", 400);
            if (reason == "other" && string.IsNullOrEmpty(customReason))
                throw new UserFriendlyException("يرجى كتابة وصف السبب", 400);

            // ⚠️ Scoped: قائمة التحويلات كانت مقسّمة والتحويل نفسه لأ — فمشرف قسم
            //    كان يقدر ينقل طالبًا من القسم التاني بين المباني بالرقم الجامعي.
            var student = await Scoped(_students.Query())
                .FirstOrDefaultAsync(s => s.student_id == studentNumber && !s.IsDeleted)
                ?? throw new UserFriendlyException("الطالب غير موجود", 400);

            if (string.IsNullOrEmpty(student.housing_building) && string.IsNullOrEmpty(student.room_number) && string.IsNullOrEmpty(student.apartment_number))
                throw new UserFriendlyException("الطالب لا يمتلك بيانات سكن حالية", 400);

            var oldBuilding = student.housing_building ?? "";
            var oldFloor = student.floor_number ?? "";
            var oldApartment = student.apartment_number ?? "";
            var oldRoom = student.room_number ?? "";

            // ⚠️ الحدّ الأقصى مسموح هنا: النقل إجراء إداري بيعمله المشرف، وإدارة
            //    الإسكان قالت صراحة إنه يقدر يزوّد ساكنًا فوق السعة المعتمدة.
            //    والطالب نفسه مستثنى من العدّ - نقله لغرفته الحالية مايحسبش
            //    مرتين (بيحصل لما المشرف يصحّح رقم دور بس).
            await _capacity.EnsureAsync(newBuilding, newFloor, newApartment, newRoom,
                                        excludeStudentId: student.Id, allowExceptionSlot: true);

            // نفس الفحص الثلاثي: الامتداد ثم الحجم ثم بصمة البايتات الأولى
            if (file != null)
                AttachmentPolicy.ValidateAndResolve(file);

            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            var clientIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

            var transfer = new HousingTransfer
            {
                StudentId = student.Id,
                StudentNumber = student.student_id,
                OldBuilding = oldBuilding,
                OldFloor = oldFloor,
                OldApartment = oldApartment,
                OldRoom = oldRoom,
                NewBuilding = newBuilding,
                NewFloor = newFloor,
                NewApartment = newApartment,
                NewRoom = newRoom,
                Reason = reason,
                CustomReason = reason == "other" ? customReason : null,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            };

            await _transfers.AddAsync(transfer);
            await UnitOfWork.SaveAsync();

            student.housing_building = newBuilding;
            student.floor_number = newFloor;
            student.apartment_number = newApartment;
            student.room_number = newRoom;

            var reasonAr = reason switch
            {
                "student_request" => "طلب الطالب",
                "major_change" => "تغيير تخصص",
                "housing_issue" => "مشكلة سكنية",
                "room_maintenance" => "صيانة غرفة",
                "admin_decision" => "قرار الإدارة",
                "other" => customReason ?? "أخرى",
                _ => reason
            };

            await _lifecycle.AddAsync(new AccountLifecycleLog
            {
                StudentId = student.Id,
                Action = "housing_transfer",
                PerformedBy = actorId,
                PerformedAt = DateTime.UtcNow,
                Details = $"نقل سكن طالب: {Loc(oldBuilding, oldFloor, oldApartment, oldRoom)} -> {Loc(newBuilding, newFloor, newApartment, newRoom)} - السبب: {reasonAr}",
                IpAddress = clientIp
            });

            // حفظ المرفق (لو فيه) — المسار بيرجع نسبيًا للجذر وبيتخزّن كامل
            if (file != null)
            {
                transfer.AttachmentPath = await _storage.SaveAsync(
                    AttachmentStorage.HousingTransfer,
                    student.student_id ?? "",
                    "transfer",
                    transfer.Id,
                    _http.HttpContext?.User?.Identity?.Name,
                    file);
                transfer.OriginalFileName = file.FileName;
            }

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("housing_transfer", "HousingTransfers", transfer.Id);

            return new TransferResultDto
            {
                Message = "تم نقل الطالب بنجاح",
                TransferId = transfer.Id,
                OldLocation = Loc(oldBuilding, oldFloor, oldApartment, oldRoom),
                NewLocation = Loc(newBuilding, newFloor, newApartment, newRoom)
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  تحميل/معاينة مرفق النقل
        // ---------------------------------------------------------------------
        //  الملف بيتخزّن باسم GUID (AttachmentPath) والاسم الأصلي بيتخزّن جنبه
        //  (OriginalFileName). الواجهة كانت بتبني الرابط بالاسم الأصلي فبيطلع 404،
        //  لأن ده مش اسم الملف على الديسك.
        //
        //  وكمان: الملفات دي وثائق طلاب. لما كانت بتتقدّم كملف ثابت من wwwroot
        //  كان أي حد معاه الرابط يفتحها من غير تسجيل دخول. دلوقتي بتعدّي من هنا
        //  فبتتحقق من الصلاحية الأول، والوصول المباشر لـ /uploads مقفول في Program.cs.
        public async Task<DownloadFileDto> GetAttachmentAsync(int transferId)
        {
            var transfer = await _transfers.Query().AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == transferId)
                ?? throw UserFriendlyException.NotFound("سجل النقل غير موجود");

            if (string.IsNullOrWhiteSpace(transfer.AttachmentPath))
                throw UserFriendlyException.NotFound("لا يوجد مرفق لهذا السجل");

            // AttachmentPath اسم ملف مولّد بالسيرفر، بس بنتأكد إنه مايحتويش مسار
            // عشان أي صف قديم أو معدّل يدويًا مايقدرش يخرج بره مجلد الرفع.
            var filePath = _storage.ResolveExisting(AttachmentStorage.HousingTransfer, transfer.Id, transfer.AttachmentPath)
                ?? throw UserFriendlyException.NotFound("الملف غير موجود على الخادم");

            // ⚠️ كان FileExtensionContentTypeProvider - وهو يعرف مئات الأنواع
            //    ومنها text/html. القائمة المغلقة أضيق وأأمن: ما ليس فيها يخرج
            //    كملف ثنائي يُنزَّل ولا يُعرض.
            return new DownloadFileDto
            {
                FilePath = filePath,
                ContentType = AttachmentPolicy.ResolveContentType(filePath),
                OriginalFileName = string.IsNullOrWhiteSpace(transfer.OriginalFileName)
                    ? Path.GetFileName(filePath)
                    : transfer.OriginalFileName
            };
        }

        // وصف موقع مقروء — «مبنى 69 · الدور 3 · شقة 15 · غرفة 3».
        // كل جزء بليبله: "69/3/15/3" لوحدها مالهاش معنى لحد ما القارئ يحفظ الترتيب.
        // "0" بتتعرض "الأرضي"، والأجزاء الفاضية بتتشال.
        // ملحوظة: النص ده بيتخزّن في AuditLog وبيترجع في RecentTransferDto، يعني
        // عربي ثابت مش متعدد اللغات — نفس سلوك باقي نصوص السجل هنا.
        private const string LocSeparator = " · ";

        private static string Loc(string? building, string? floor, string? apartment, string? room)
        {
            var parts = new List<string>(4);
            if (!string.IsNullOrWhiteSpace(building)) parts.Add($"مبنى {building.Trim()}");
            if (!string.IsNullOrWhiteSpace(floor))
                parts.Add($"الدور {(floor.Trim() == "0" ? "الأرضي" : floor.Trim())}");
            if (!string.IsNullOrWhiteSpace(apartment)) parts.Add($"شقة {apartment.Trim()}");
            if (!string.IsNullOrWhiteSpace(room)) parts.Add($"غرفة {room.Trim()}");
            return string.Join(LocSeparator, parts);
        }

        // حجم الصفحة الواحدة من سجل التنقلات - نفس رقم سجل التحديثات.
        public const int RecentPageSize = 50;

        public async Task<List<RecentTransferDto>> GetRecentAsync(int skip = 0)
        {
            if (UnitOfWork.GetCurrentUserId() == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            // ملحوظة: ضفنا Include(Student) — الكود القديم كان بيرجّع StudentName = null دايمًا (باج)
            // وشلنا الفلتر Where(CreatedBy == actorId): كان بيخلّي كل مستخدم يشوف
            // تنقلاته هو بس، فالأدمن كان بيلاقي السجل فاضي تمامًا رغم إنه المفروض
            // يشوف كل حاجة. عمود «بواسطة» بيوضّح مين عمل كل نقل.
            // سجل النقل بيتقيّد بالقسم زي أي شاشة تانية.
            // ⚠️ الشرطية اتنقلت لـ Core/GenderScope.cs - نسخة واحدة لكل السجلات.
            IQueryable<HousingTransfer> q = _transfers.Query().AsNoTracking()
                .ForGender(UnitOfWork.GetGenderScope())
                .Include(t => t.Student)
                .Include(t => t.CreatedByUser);

            return await q
                .OrderByDescending(t => t.CreatedAt)
                .Skip(Math.Max(0, skip))
                .Take(RecentPageSize)
                .Select(t => new RecentTransferDto
                {
                    Id = t.Id,
                    StudentNumber = t.StudentNumber,
                    OldBuilding = t.OldBuilding,
                    OldFloor = t.OldFloor,
                    OldApartment = t.OldApartment,
                    OldRoom = t.OldRoom,
                    NewBuilding = t.NewBuilding,
                    NewFloor = t.NewFloor,
                    NewApartment = t.NewApartment,
                    NewRoom = t.NewRoom,
                    Reason = t.Reason,
                    CustomReason = t.CustomReason,
                    CreatedAt = t.CreatedAt,
                    CreatedBy = t.CreatedBy,
                    AttachmentPath = t.AttachmentPath,
                    OriginalFileName = t.OriginalFileName,
                    StudentName = t.Student != null ? t.Student.full_name : null,
                    CreatedByName = t.CreatedByUser != null
                        ? (t.CreatedByUser.full_name ?? t.CreatedByUser.UserName)
                        : null
                })
                .ToListAsync();
        }
    }
}
