using MapsterMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;
using System.Text.RegularExpressions;

namespace NUH_PORTAL.Services
{
    // كل منطق الطلاب اتنقل هنا من الكنترولر (Controller بقى رفيع بيوجّه بس)
    public class StudentService : AppServiceBase, IStudentService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Request> _requests;
        private readonly IRepository<AccountLifecycleLog> _lifecycle;
        private readonly IAuditService _audit;
        private readonly ILookupResolver _lookups;

        // ⚠️ كانت هنا قائمة مباني مكتوبة بالإيد:
        //       { "40","41","42","43","65","66","67","68","69","70" }
        //    والمباني **جدول مُدار** من شاشة القوائم المرجعية. فالمدير يضيف
        //    مبنى ٧١، يلاقيه في القايمة المنسدلة في شاشة التسجيل (لأنها بتقرا
        //    من الجدول)، يختاره، ويضغط حفظ - فيترفض برسالة «رقم المبنى غير
        //    صحيح، القيم المسموح بها: ٤٠،٤١...». يعني النظام بيعرض له خيارًا
        //    ويرفضه هو نفسه، ومفيش أي طريقة يفهم منها السبب.
        //
        //    الفحص بقى بيقرا من نفس الجدول اللي القايمة بتقرا منه.

        public StudentService(
            IRepository<Student> students,
            IRepository<Request> requests,
            IRepository<AccountLifecycleLog> lifecycle,
            IAuditService audit,
            ILookupResolver lookups,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _requests = requests;
            _lifecycle = lifecycle;
            _audit = audit;
            _lookups = lookups;
        }

        public async Task<List<StudentDto>> GetStudentsAsync(bool showDeleted, string? adStatus)
        {
            var list = await BuildStudentsQuery(showDeleted, adStatus).ToListAsync();
            return Mapper.Map<List<StudentDto>>(list);
        }

        public async Task<QueryResult<StudentDto>> GetPagedAsync(QueryParams queryParams, bool showDeleted, string? adStatus)
        {
            var query = BuildStudentsQuery(showDeleted, adStatus);

            // بحث حر في أهم الحقول
            var f = queryParams.FilterText?.Trim();
            if (!string.IsNullOrEmpty(f))
            {
                query = query.Where(s =>
                    (s.student_id != null && s.student_id.Contains(f)) ||
                    (s.full_name != null && s.full_name.Contains(f)) ||
                    (s.full_name_english != null && s.full_name_english.Contains(f)) ||
                    (s.national_id != null && s.national_id.Contains(f)) ||
                    (s.phone != null && s.phone.Contains(f)) ||
                    (s.college != null && s.college.Contains(f)));
            }

            // ترتيب
            query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortAsc) switch
            {
                ("student_id", true) => query.OrderBy(s => s.student_id),
                ("student_id", false) => query.OrderByDescending(s => s.student_id),
                ("full_name", true) => query.OrderBy(s => s.full_name),
                ("full_name", false) => query.OrderByDescending(s => s.full_name),
                ("college", true) => query.OrderBy(s => s.college),
                ("college", false) => query.OrderByDescending(s => s.college),
                ("created_at", true) => query.OrderBy(s => s.created_at),
                ("created_at", false) => query.OrderByDescending(s => s.created_at),
                ("status", true) => query.OrderBy(s => s.status),
                ("status", false) => query.OrderByDescending(s => s.status),
                ("id", true) => query.OrderBy(s => s.Id),
                _ => query.OrderByDescending(s => s.Id)
            };

            var result = await query.ToPagedResultAsync(queryParams);
            return result.Map<Student, StudentDto>(Mapper);
        }

        // ====================================================================
        //  عدد الساكنين في كل مبنى — لرسم الصفحة الرئيسية عند المشرف.
        //
        //  ⚠️ يمرّ بـ BuildStudentsQuery لا باستعلام مستقلّ: القاعدة التي تخفي
        //     المحذوفين وتفصل الطلاب عن الطالبات مكتوبة هناك مرة واحدة. لو
        //     كُتب هنا استعلام ثانٍ لرأت المشرفة عدد الطلاب في مباني الطلاب،
        //     وهو نفس التسريب الذي أُغلق في قوائم الطلاب.
        //
        //  ⚠️ والتجميع على الخادم لا في المتصفح: جلب كل الطلاب لعدّهم في
        //     الواجهة يعني نقل السجلّ كاملًا لرسم فيه أربعة أعمدة.
        // ====================================================================
        public async Task<List<BuildingCountDto>> GetCountByBuildingAsync()
        {
            return await BuildStudentsQuery(false, null)
                .Where(s => s.housing_building != null && s.housing_building != "")
                .GroupBy(s => s.housing_building!)
                .Select(g => new BuildingCountDto { Building = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Building)
                .ToListAsync();
        }

        private IQueryable<Student> BuildStudentsQuery(bool showDeleted, string? adStatus)
        {
            // ⚠️ تقسيم الطلاب/الطالبات - كان غائبًا عن قوائم الطلاب كلّها، فترى
            //    المشرفة أسماء الطلاب وأرقام هوياتهم وجوالاتهم وسكنهم. القاعدة
            //    في Core/GenderScope.cs، هي نفسها التي تفلتر بها شاشة الطلبات.
            var query = _students.Query().AsNoTracking()
                .ForGender(UnitOfWork.GetGenderScope());
            if (!showDeleted)
                query = query.Where(s => !s.IsDeleted);

            if (!string.IsNullOrEmpty(adStatus))
            {
                query = adStatus.ToLower() switch
                {
                    "enabled" => query.Where(s => s.ad_status == AdStatus.enabled),
                    "disabled" => query.Where(s => s.ad_status == AdStatus.disabled),
                    "none" => query.Where(s => s.ad_status == null),
                    // ⚠️ "missing" غير "none": الأخيرة تعني «بلا حساب» حرفيًّا فتشمل
                    //    من غادر السكن - وحسابه أُغلق أو لم يُنشأ عمدًا، فلا شيء
                    //    يُفعل حياله. أما "missing" فهي الحالة الشاذة وحدها:
                    //    ساكن حالي بلا حساب، وغالبًا فشل إنشاء يحتاج متابعة.
                    //    وهي القيمة التي تقف خلفها شارة «بلا حساب» في القائمة،
                    //    فالشارة والفلتر يقرآن التعريف نفسه لا تعريفين متشابهين.
                    "missing" => query.Where(s => s.ad_status == null && s.status != StudentState.left),
                    "any" => query.Where(s => s.ad_status != null),
                    _ => query
                };
            }

            return query;
        }

        public async Task<StudentStatsDto> GetStatsAsync()
        {
            // ⚠️ عدّادات لوحة التحكم كانت بلا تقسيم: «إجمالي الطلاب» و«يحتاج
            //    إجراءك» تُحسب على النظام كلّه، فيرى المشرف أرقامًا تشمل القسم
            //    الآخر - ولا سبيل له إلى معرفة أن الرقم ليس رقمه.
            var scope = UnitOfWork.GetGenderScope();

            // عدّادات الطلاب (شروط مركّبة) — استعلامات منفصلة
            var students = _students.Query().AsNoTracking().ForGender(scope);
            var total = await students.CountAsync(s => !s.IsDeleted);
            var active = await students.CountAsync(s => s.status == StudentState.active && !s.IsDeleted);
            var left = await students.CountAsync(s => s.status == StudentState.left && !s.IsDeleted);
            // ⚠️ نفس شرط total بالحرف (غير المحذوفين) عشان male + female = total.
            //    والاتنين ماشيين على ForGender زي كل حاجة هنا: المشرف بيشوف
            //    قسمه في العدّاد والتاني صفر — وده صحيح لا ناقص.
            var male = await students.CountAsync(s => s.gender == Gender.Male && !s.IsDeleted);
            var female = await students.CountAsync(s => s.gender == Gender.Female && !s.IsDeleted);

            // كل حالات الطلبات في استعلام GroupBy واحد بدل 8 استعلامات منفصلة
            var reqCounts = (await _requests.Query().AsNoTracking()
                    .ForGender(scope)
                    .GroupBy(r => r.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync())
                .Where(x => x.Status != null)
                .ToDictionary(x => x.Status!, x => x.Count);

            // ⚠️ العدّاد بيجمع مسمّيات المرحلة الواحدة من RequestWorkflow.Aliases —
            //    نفس دالة Of في RequestService بالحرف. كانت مكتوبة هنا بالإيد
            //    (Req("submitted") + Req("pending_supervisor") وهكذا)، يعني نفس
            //    القاعدة في مكانين: شاشة الطلبات بتقرا من الجدول ولوحة التحكم
            //    بتقرا من قايمة مكتوبة. أي مرحلة جديدة كانت هتظهر في شاشة
            //    وتختفي من التانية، والفرق ما بيبانش غير لو حد قارن الرقمين.
            int Req(string status)
            {
                var names = RequestWorkflow.Aliases(status);
                return names.Sum(n => reqCounts.TryGetValue(n, out var c) ? c : 0);
            }

            // ⚠️ النظام فيه مسارين بمصطلحات مختلفة لنفس المراحل:
            //    طلبات الموظفين  : submitted / cyber_review / cyber_approved / completed
            //    تسجيل الطالب الذاتي: pending_supervisor / pending_cyber / ready_for_provisioning / approved
            // العدّادات كانت بتقرا مصطلحات الموظفين بس، فطلب الطالب اللي مستني
            // المشرف مكانش بيتعدّ خالص — المشرف يشوف «مفيش طلبات مستنية إجراء منك»
            // وفي نفس الوقت الطلب ظاهر في «آخر الطلبات» بحالة «بانتظار الإسكان».
            // التجميع بقى من RequestWorkflow.Aliases فوق، مش بجمع مكتوب هنا.
            //
            // ⚠️ و«rejected» بقى ليه عدّاد: كان التعليق القديم بيقول إنها مستثناة
            //    عن قصد لأنها مابتقولش الرفض جه منين — وده صحيح كوصف، لكن نتيجته
            //    إن الطلب المرفوض من مسار الطالب مكانش بيتعدّ في **أي** خانة.
            //    شاشة الطلبات بتعدّه في تبويب «مرفوض» من أول يوم، فالرقمان كانا
            //    بيختلفا. العدّاد هنا بيجمع الرفض من المسارين زي التبويب بالظبط.
            return new StudentStatsDto
            {
                total = total,
                male = male,
                female = female,
                active = active,
                left = left,
                submitted = Req("submitted"),
                housing_approved = Req("housing_approved"),
                housing_rejected = Req("housing_rejected"),
                cyber_review = Req("cyber_review"),
                cyber_approved = Req("cyber_approved"),
                cyber_rejected = Req("cyber_rejected"),
                ready_for_provisioning = Req("ready_for_provisioning"),
                completed = Req("completed"),
                // ⚠️ الرفض من المسارين مجمّعًا — نفس تبويب «مرفوض» في شاشة الطلبات.
                rejected = Req("rejected"),
            };
        }

        public async Task<StudentDto> GetByIdAsync(int id)
        {
            // ⚠️ Scoped بدل _students.GetByIdAsync: الأخيرة بتتجاهل تقسيم
            //    القسم، فمشرف قسم كان بيقرا ويعدّل سجل من القسم التاني بالمعرّف.
            //    «غير موجود» لا «ممنوع» عن قصد: الرد ما يقولش إن السجل موجود
            //    في قسم تاني.
            var student = await Scoped(_students.Query()).AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");
            return Mapper.Map<StudentDto>(student);
        }

        // ⚠️ شاشات الإدخال كانت بتسحب *كل* الطلاب وتدوّر فيهم في المتصفح على كل
        //    خروج من خانة الرقم الجامعي. مع 15 طالب تجريبي مبانش، ومع آلاف طالب
        //    ده تحميل ميجابايتات على كل ضغطة. البحث بقى صف واحد من قاعدة البيانات.
        public async Task<StudentDto?> GetByStudentNumberAsync(string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
                return null;

            var trimmed = studentNumber.Trim();
            var student = await Scoped(_students.Query()).AsNoTracking()
                .FirstOrDefaultAsync(s => s.student_id == trimmed && !s.IsDeleted);

            return student == null ? null : Mapper.Map<StudentDto>(student);
        }

        public async Task<StudentDto> CreateAsync(StudentCreateDto dto)
        {
            EnsureNotReadonlyUser();

            // ⚠️ الجنس بياخده من قسم الموظف مش من الفورم. الشاشة بتحدّد الخانة
            //    تلقائيًا وتقفلها، لكن القفل ده راحة للمستخدم مش حراسة — نداء
            //    واحد بالـ API كان بينشئ طالبة تحت قسم الطلاب، فتختفي عن مشرف
            //    الطلاب (خرجت عن نطاقه) وعن المشرفة (سجل اتعمل غلط أصلًا).
            //    القاعدة نفسها في Core/GenderScope.cs.
            var gender = GenderScope.Resolve(UnitOfWork.GetGenderScope(), dto.gender);

            var errors = Validate(dto.full_name, dto.full_name_english, dto.student_id, dto.national_id, dto.phone, dto.housing_building);
            await AddBuildingErrorAsync(dto.housing_building, errors);
            // ⚠️ الطالب بلا جنس ما بيوصلش لا لمشرف قسم الطلاب ولا لمشرفة قسم
            //    الطالبات — نفس السبب اللي خلّى الخانة إجبارية في رفع الإكسل.
            //    عمليًا ده بيخصّ الأدمن وحده، لأن أي مشرف بياخد جنسه من قسمه.
            if (gender == null)
                errors.Add("الجنس مطلوب - الطلب بيتوجّه لمشرف القسم بناءً عليه");
            if (await _students.ExistsAsync(s => s.student_id == dto.student_id && !s.IsDeleted))
                errors.Add("الرقم الجامعي موجود بالفعل");
            if (await _students.ExistsAsync(s => s.national_id == dto.national_id && !s.IsDeleted))
                errors.Add("رقم الهوية موجود بالفعل");
            if (errors.Count > 0)
                throw new UserFriendlyException(string.Join(" | ", errors), 400);

            var student = Mapper.Map<Student>(dto);
            student.created_at = DateTime.UtcNow;
            student.status ??= StudentState.active;
            student.student_status ??= StudentStatus.active;
            student.created_by = UnitOfWork.GetCurrentUserId();
            // بعد الـ Mapper عن قصد: القيمة المفروضة فوق هي اللي بتتخزّن، مش
            // اللي جاية من الـ dto. (التطبيع لـ enum بيحصل في الـ JsonConverter
            // وقت الاستقبال — مفيش تطبيع يدوي محتاج هنا.)
            student.gender = gender;
            await _lookups.ApplyAsync(student); // FK ids من الأكواد (dual-write)

            try
            {
                await _students.AddAsync(student);
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
            {
                throw new UserFriendlyException("بيانات مكررة: رقم الهوية أو الرقم الجامعي موجود بالفعل", 409);
            }

            await _audit.LogAsync("create_student", "Students", student.Id);

            return Mapper.Map<StudentDto>(student);
        }

        public async Task<StudentDto> UpdateAsync(int id, StudentUpdateDto dto)
        {
            EnsureNotReadonlyUser();

            // ⚠️ Scoped بدل _students.GetByIdAsync: الأخيرة بتتجاهل تقسيم
            //    القسم، فمشرف قسم كان بيقرا ويعدّل سجل من القسم التاني بالمعرّف.
            //    «غير موجود» لا «ممنوع» عن قصد: الرد ما يقولش إن السجل موجود
            //    في قسم تاني.
            var student = await Scoped(_students.Query())
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");

            var errors = Validate(dto.full_name, dto.full_name_english, dto.student_id, dto.national_id, dto.phone, dto.housing_building);
            await AddBuildingErrorAsync(dto.housing_building, errors);
            if (errors.Count > 0)
                throw new UserFriendlyException(string.Join(" | ", errors), 400);

            // ⚠️ تغيير جنس طالب قائم = نقله لقسم تاني، فيختفي من قائمة اللي
            //    بيتابعه ويظهر لواحد ما يعرفوش. مسموح لمن نطاقه «القسمين»
            //    (الأدمن) وحده. ومشرف القسم أصلًا ما بيوصلش لسجل من القسم
            //    التاني — فالحالة الوحيدة الممكنة عنده هي إخراج طالب من قسمه.
            //    القاعدة في Core/GenderScope.cs.
            if (dto.gender != null && dto.gender != student.gender
                && !GenderScope.CanWrite(UnitOfWork.GetGenderScope(), dto.gender))
                throw new UserFriendlyException("تغيير الجنس غير متاح - القسم بيتحدد من إدارة النظام", 403);

            // تفرّد رقم الهوية عند التعديل — مع استثناء الطالب نفسه.
            // (كان ناقص: التعديل ماكانش بيتأكد إن رقم الهوية مش مستخدم لطالب تاني — بق بيسمح بالتكرار.)
            if (!string.IsNullOrEmpty(dto.national_id) && dto.national_id != student.national_id
                && await _students.ExistsAsync(s => s.national_id == dto.national_id && s.Id != id && !s.IsDeleted))
                throw new UserFriendlyException("رقم الهوية موجود بالفعل لطالب آخر", 409);

            var changes = new List<AuditChangeLog>();
            var fields = new (string field, string? oldVal, string? newVal)[]
            {
                ("full_name", student.full_name, dto.full_name),
                ("full_name_english", student.full_name_english, dto.full_name_english),
                ("national_id", student.national_id, dto.national_id),
                ("phone", student.phone, dto.phone),
                ("gender", GenderHelper.ToStr(student.gender), GenderHelper.ToStr(dto.gender)),
                ("college", student.college, dto.college),
                ("department", student.department, dto.department),
                ("academic_level", student.academic_level, dto.academic_level),
                ("housing_building", student.housing_building, dto.housing_building),
                ("floor_number", student.floor_number, dto.floor_number),
                ("room_number", student.room_number, dto.room_number),
                ("apartment_number", student.apartment_number, dto.apartment_number),
                ("status", student.status?.ToString(), dto.status?.ToString()),
            };

            bool modified = false;
            foreach (var (field, oldVal, newVal) in fields)
            {
                if (!string.IsNullOrEmpty(newVal) && oldVal != newVal)
                {
                    if (field == "full_name" && !string.IsNullOrEmpty(dto.full_name)) student.full_name = dto.full_name;
                    else if (field == "full_name_english") student.full_name_english = dto.full_name_english;
                    else if (field == "national_id" && !string.IsNullOrEmpty(dto.national_id)) student.national_id = dto.national_id;
                    else if (field == "phone" && !string.IsNullOrEmpty(dto.phone)) student.phone = dto.phone;
                    else if (field == "gender" && dto.gender != null) student.gender = dto.gender;
                    else if (field == "college" && !string.IsNullOrEmpty(dto.college)) student.college = dto.college;
                    else if (field == "department") student.department = dto.department;
                    else if (field == "academic_level") student.academic_level = dto.academic_level;
                    else if (field == "housing_building" && !string.IsNullOrEmpty(dto.housing_building)) student.housing_building = dto.housing_building;
                    else if (field == "floor_number") student.floor_number = dto.floor_number;
                    else if (field == "room_number" && !string.IsNullOrEmpty(dto.room_number)) student.room_number = dto.room_number;
                    else if (field == "apartment_number") student.apartment_number = dto.apartment_number;
                    else if (field == "status" && dto.status != null) student.status = dto.status;

                    changes.Add(new AuditChangeLog { FieldName = field, OldValue = oldVal, NewValue = newVal });
                    modified = true;
                }
            }

            if (!modified)
                return Mapper.Map<StudentDto>(student);

            await _lookups.ApplyAsync(student); // إعادة حساب الـ FK ids بعد تغيّر الأكواد

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
            {
                // خط دفاع تاني لو فيه unique index على رقم الهوية على مستوى الداتابيز
                throw new UserFriendlyException("بيانات مكررة: رقم الهوية موجود بالفعل", 409);
            }
            await _audit.LogAsync("update_student", "Students", student.Id, changes);

            return Mapper.Map<StudentDto>(student);
        }

        public async Task DeleteAsync(int id)
        {
            EnsureCanDelete("غير مسموح لك بحذف الطالب. يرجى التواصل مع مسؤول النظام.");

            var student = await Scoped(_students.Query())
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");
            if (student.IsDeleted)
                throw new UserFriendlyException("الطالب محذوف بالفعل", 400);

            var actorId = UnitOfWork.GetCurrentUserId();
            student.IsDeleted = true;
            student.DeletedBy = actorId > 0 ? actorId : (int?)null;
            student.DeletedDate = DateTime.UtcNow;

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("soft_delete_student", "Students", id);
        }

        public async Task RestoreAsync(int id)
        {
            EnsureCanDelete("غير مسموح لك باستعادة الطالب. يرجى التواصل مع مسؤول النظام.");

            var student = await Scoped(_students.Query())
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");
            if (!student.IsDeleted)
                throw new UserFriendlyException("الطالب غير محذوف", 400);

            var actorId = UnitOfWork.GetCurrentUserId();
            student.IsDeleted = false;
            student.RestoredBy = actorId > 0 ? actorId : (int?)null;
            student.RestoredDate = DateTime.UtcNow;
            student.DeletedBy = null;
            student.DeletedDate = null;

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("restore_student", "Students", id);
        }

        public async Task<List<LifecycleLogDto>> GetLifecycleAsync(int id)
        {
            // ⚠️ سجل دورة الحياة مالوش عمود جنس — القسم قسم صاحبه. فبنتأكد إن
            //    الطالب نفسه داخل نطاق المستخدم قبل ما نرجّع أي سطر، وإلا بقى
            //    السجل بابًا خلفيًّا لقراءة نشاط طالب من القسم التاني.
            var inScope = await Scoped(_students.Query()).AsNoTracking()
                .AnyAsync(s => s.Id == id);
            if (!inScope)
                throw UserFriendlyException.NotFound("الطالب غير موجود");

            return await _lifecycle.Query().AsNoTracking()
                .Where(l => l.StudentId == id)
                .OrderByDescending(l => l.PerformedAt)
                .Take(50)
                .Select(l => new LifecycleLogDto
                {
                    Id = l.Id,
                    Action = l.Action,
                    PerformedAt = l.PerformedAt,
                    Details = l.Details,
                    IpAddress = l.IpAddress,
                    PerformerName = l.Performer != null ? l.Performer.full_name : ""
                })
                .ToListAsync();
        }

        // ----------------------------- Helpers -----------------------------

        // طبقة تانية تحت فحص الكنترولر: لازم يكون معاه صلاحية إضافة أو تعديل.
        // (الكنترولر بيفرّق بين الاتنين؛ ده حاجز أخير لو حد نادى الـ service من مكان تاني.)
        private void EnsureNotReadonlyUser()
        {
            if (!UnitOfWork.HasPermission("students.create")
                && !UnitOfWork.HasPermission("students.edit"))
                throw UserFriendlyException.Forbidden();
        }

        // الحذف/الاستعادة بصلاحية students.delete — كانت مقفولة على دور admin،
        // فأي دور جديد كان بيلاقي الزر شغّال والـ API بيرفض.
        private void EnsureCanDelete(string message)
        {
            if (!UnitOfWork.HasPermission("students.delete"))
                throw UserFriendlyException.Forbidden(message);
        }

        // فحص المبنى من ILookupResolver - التعريف الوحيد (الشرح في الواجهة).
        private async Task AddBuildingErrorAsync(string? housingBuilding, List<string> errors)
        {
            var err = await _lookups.BuildingCodeErrorAsync(housingBuilding);
            if (err != null) errors.Add(err);
        }

        private static List<string> Validate(string? fullName, string? fullNameEn, string? studentId, string? nationalId, string? phone, string? housingBuilding)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(fullName) || !Regex.IsMatch(fullName, @"^[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\s]+$"))
                errors.Add("الاسم بالعربية: يرجى إدخال الاسم باللغة العربية فقط");
            if (string.IsNullOrEmpty(fullNameEn) || !Regex.IsMatch(fullNameEn, @"^[a-zA-Z\s]+$"))
                errors.Add("الاسم بالإنجليزية: يرجى إدخال الاسم باللغة الإنجليزية فقط");
            // الرقم الجامعي في جامعة نجران: ٩ أرقام بالظبط وبيبدأ بـ 4.
            // كان ^\d{9,10}$ — بيقبل ١٠ أرقام وبيقبل أي بداية.
            // ⚠️ الأنماط والرسائل من Core/IdentityRules — كانت مكتوبة هنا بالإيد،
            //    ونسخة الهوية هنا كانت **أضعف** من نسخة سكن أعضاء هيئة التدريس
            //    (أي عشرة أرقام مقابل عشرة تبدأ بـ ١ أو ٢). فرقم جوال في خانة
            //    الهوية كان بيترفض في شاشة ويتقبل في شاشة تانية.
            if (!IdentityRules.IsValidStudentId(studentId))
                errors.Add(IdentityRules.StudentIdError);
            if (IdentityRules.LooksLikeMobile(nationalId))
                errors.Add(IdentityRules.NationalIdIsMobileError);
            else if (!IdentityRules.IsValidNationalId(nationalId))
                errors.Add(IdentityRules.NationalIdError);
            if (!string.IsNullOrEmpty(phone) && !Regex.IsMatch(phone, IdentityRules.StoredMobilePattern))
                errors.Add(IdentityRules.MobileError);
            return errors;
        }
    }
}
