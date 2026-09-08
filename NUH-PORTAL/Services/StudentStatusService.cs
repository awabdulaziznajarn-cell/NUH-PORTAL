using MapsterMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Attachments;
using NUH_PORTAL.DTOs.StudentStatus;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // كل منطق حالات المغادرة اتنقل هنا من StudentStatusController
    public class StudentStatusService : AppServiceBase, IStudentStatusService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<StudentStatusAction> _actions;
        private readonly IRepository<AccountLifecycleLog> _lifecycle;
        private readonly IRepository<StudentStatusAttachment> _attachments;
        // ⚠️ إخلاء السكن يمرّ بطريق التسكين الواحد: هو يفرّغ الحقول الخمسة
        //    ويكتب صفّ الحركة في سجل واحد (HousingHistory)، فيُجاب سؤال «من
        //    سكن في هذه الغرفة» من مصدر واحد مهما كان سبب المغادرة.
        private readonly IHousingPlacement _placement;
        private readonly ActiveDirectoryService _adService;
        private readonly IAttachmentStorage _storage;
        private readonly IHttpContextAccessor _http;
        private readonly IAuditService _audit;

        // ⚠️ قائمة الامتدادات وحدّ الحجم وفحص البصمة كلها في Core/AttachmentPolicy
        //    الآن. كانت مكتوبة هنا وفي SupervisorHousingTransferService، فاختلف
        //    المساران في طريقة اشتقاق نوع المحتوى - وهناك وقعت الثغرة.
        public const long MaxFileSize = AttachmentPolicy.MaxFileSize;

        // ⚠️ "other" مضافة هنا كمان — من غيرها الشاشة تبعت القيمة والسيرفر يرفضها.
        private static readonly string[] ValidStatuses = { "graduated", "dismissed", "transferred", "left_housing", "other" };

        // الحالات اللي بتنهي علاقة الطالب بالجامعة — دي اللي مايتكررش.
        // «ترك الإسكان الجامعي» مش منها: الطالب سايب السكن بس ولسه على رأس عمله.
        private static bool IsFinalStatus(string? st) =>
            st is "graduated" or "dismissed" or "transferred";

        // student_status نوعه StudentStatus? — لازم النسخة دي تقبل null
        private static bool IsFinalStatus(StudentStatus? st) =>
            st is StudentStatus.graduated or StudentStatus.dismissed or StudentStatus.transferred;

        private static string StatusLabel(StudentStatus? st) => st switch
        {
            StudentStatus.graduated => "تخرج من الكلية",
            StudentStatus.dismissed => "فصل من الكلية",
            StudentStatus.transferred => "تحويل إلى جامعة أخرى",
            StudentStatus.left_housing => "ترك الإسكان الجامعي",
            StudentStatus.other => "أخرى",
            _ => st?.ToString() ?? ""
        };

        public StudentStatusService(
            IRepository<Student> students,
            IRepository<StudentStatusAction> actions,
            IRepository<AccountLifecycleLog> lifecycle,
            IRepository<StudentStatusAttachment> attachments,
            IHousingPlacement placement,
            ActiveDirectoryService adService,
            IAttachmentStorage storage,
            IHttpContextAccessor http,
            IAuditService audit,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _actions = actions;
            _lifecycle = lifecycle;
            _attachments = attachments;
            _placement = placement;
            _adService = adService;
            _storage = storage;
            _http = http;
            _audit = audit;
        }

        public async Task<StudentStatusResultDto> CreateStatusActionAsync(string studentNumber, string statusType, string notes, IFormFile? file)
        {
            if (!UnitOfWork.HasPermission("students.changeStatus"))
                throw UserFriendlyException.Forbidden();

            return await CreateCoreAsync(studentNumber, statusType, notes, file,
                detailsPrefix: "تحديث حالة الطالب",
                invalidStatusMessage: "نوع الحالة غير صحيح - القيم المسموح بها: تخرج, فصل من الكلية, تحويل إلى جامعة أخرى, ترك الإسكان الجامعي");
        }

        private async Task<StudentStatusResultDto> CreateCoreAsync(string studentNumber, string statusType, string notes, IFormFile? file, string detailsPrefix, string invalidStatusMessage)
        {

            if (string.IsNullOrEmpty(studentNumber))
                throw new UserFriendlyException("الرقم الجامعي مطلوب", 400);

            if (string.IsNullOrEmpty(notes) || notes.Trim().Length < 5)
                throw new UserFriendlyException("سبب الإجراء إلزامي (5 أحرف على الأقل)", 400);

            // ⚠️ Scoped: الإحصاءات كانت مقسّمة والإجراء نفسه لأ — فمشرف قسم كان
            //    يقدر يسجّل تخرّجًا أو فصلًا على طالب من القسم التاني بالرقم
            //    الجامعي، والإجراء ده بيعطّل حساب الطالب في الدليل.
            var student = await Scoped(_students.Query())
                .FirstOrDefaultAsync(s => s.student_id == studentNumber && !s.IsDeleted)
                ?? throw new UserFriendlyException("الطالب غير موجود", 400);

            var st = statusType?.Trim().ToLower();
            if (string.IsNullOrEmpty(st) || !ValidStatuses.Contains(st))
                throw new UserFriendlyException(invalidStatusMessage, 400);

            // ⚠️ الحالات النهائية مايتكررش: طالب اتخرّج مايتفصلش بعدها.
            //    قبل كده النظام كان بيقبل أي عدد إجراءات، فالحالة الأخيرة بتكتب
            //    فوق اللي قبلها والسجل يبقى فيه سطرين متناقضين — ومفيش إجابة
            //    على سؤال "الطالب ده اتخرّج ولا اتفصل؟".
            //
            //    الأدمن مستثنى عن قصد: لازم يكون فيه مخرج لتصحيح غلط المشرف من
            //    غير ما حد يفتح قاعدة البيانات. والتصحيح بيتسجّل باسمه في السجل.
            if (IsFinalStatus(st) && IsFinalStatus(student.student_status)
                && !UnitOfWork.HasPermission("students.overrideStatus"))
            {
                var currentAr = StatusLabel(student.student_status);
                throw new UserFriendlyException(
                    $"حالة الطالب مسجّلة بالفعل: {currentAr}. لا يمكن تسجيل حالة نهائية جديدة - راجع مسؤول النظام لتصحيحها.", 400);
            }

            // نوع المحتوى المشتقّ من الامتداد - هو ما يُخزَّن، لا قيمة العميل
            string? safeContentType = null;
            if (file != null)
                safeContentType = AttachmentPolicy.ValidateAndResolve(file);

            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            var clientIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

            var statusAr = st switch
            {
                "graduated" => "تخرج من الكلية",
                "dismissed" => "فصل من الكلية",
                "transferred" => "تحويل إلى جامعة أخرى",
                "left_housing" => "ترك الإسكان الجامعي",
                "other" => "أخرى",
                _ => st
            };

            var action = new StudentStatusAction
            {
                StudentId = student.Id,
                StudentNumber = student.student_id,
                StatusType = st,
                Notes = notes.Trim(),
                CreatedBy = actorId,
                CreatedDate = DateTime.UtcNow
            };

            var previousStatus = student.student_status?.ToString();

            student.student_status = st switch
            {
                "graduated" => StudentStatus.graduated,
                "dismissed" => StudentStatus.dismissed,
                "transferred" => StudentStatus.transferred,
                "left_housing" => StudentStatus.left_housing,
                "other" => StudentStatus.other,
                _ => student.student_status
            };
            student.status = StudentState.left;
            // ================================================================
            //  تفريغ السكن.
            //
            //  ⚠️ كان مقصورًا على «ترك الإسكان» وحده - والحالات التلاتة التانية
            //     (تخرّج، فصل، تحويل لجامعة أخرى) كانت بتسيب الغرفة مسجّلة على
            //     الطالب **للأبد**. النتيجة: ملف طالب متخرّج من سنتين لسه بيقول
            //     إنه ساكن في مبنى ٤٠، والغرفة تبان مشغولة وهي فاضية.
            //     والحالات الأربع كلها بتنهي السكن فعلًا - نفس المنطق اللي
            //     بيتعطّل بيه حساب الشبكة تحت بلا استثناء.
            //
            //  ⚠️ «أخرى» مستثناة عن قصد: هي حالة غير محدّدة والمشرف بيكتب
            //     تفاصيلها بإيده - مانعرفش لو الطالب ساب السكن ولا لأ، وتفريغ
            //     غرفة طالب لسه ساكن فيها أسوأ من ترك بيانات قديمة.
            //
            //  ⚠️ والخمس خانات مع بعض + المفتاح الأجنبي: الكود القديم كان
            //     بيمسح النصّ (housing_building) ويسيب BuildingId شايل المبنى،
            //     ويسيب floor_number كمان - فالصفّ يفضل متناقض مع نفسه.
            //
            //  ⚠️ والتفريغ **لا يمحو التاريخ**: الإخلاء يمرّ بـIHousingPlacement
            //     كما يمرّ به التسكين، فيُكتب صفّ في سجل حركة التسكين
            //     (HousingHistory) فيه الموضع المتروك وسبب المغادرة ومن نفّذ
            //     ومتى. وبغيره يبقى سؤال «من سكن في هذه الغرفة» بلا جواب.
            //
            //  ⚠️ وأُزيل من هنا صفّ «مغادرة» كان يُكتب في HousingTransfers:
            //     كان حيلة لحفظ التاريخ قبل وجود جدول السجل - صفّ نقل ليس
            //     نقلًا، بموضع جديد فارغ. وبقاؤه مع السجل يعني تسجيل الحادثة
            //     الواحدة في جدولين، وهو أوّل طريق إلى رقمين متعارضين.
            // ================================================================
            var hadHousing = !string.IsNullOrWhiteSpace(student.housing_building)
                             || !string.IsNullOrWhiteSpace(student.apartment_number)
                             || !string.IsNullOrWhiteSpace(student.room_number);

            var oldBuilding = student.housing_building ?? "";
            var oldFloor = student.floor_number ?? "";
            var oldApartment = student.apartment_number ?? "";
            var oldRoom = student.room_number ?? "";

            if (st is "left_housing" or "graduated" or "dismissed" or "transferred")
            {
                await _placement.ClearAsync(student, statusAr,
                                            HousingHistoryKinds.Sources.StatusChange);
            }

            await _actions.AddAsync(action);
            await UnitOfWork.SaveAsync();

            // ⚠️ تصحيح مقصود: كان «ترك الإسكان الجامعي» وحده لا يُعطِّل الحساب،
            //    بناءً على افتراض أن الحساب هو حساب الطالب الجامعي فيبقى محتاجًا
            //    إليه للدراسة. الافتراض خاطئ: الحساب الذي ينشئه هذا النظام هو
            //    حساب **شبكة السكن** — اسمه h + الرقم الجامعي، ويُنشأ داخل وحدة
            //    السكن التنظيمية ومجموعتها، ولا علاقة له بالحساب الأكاديمي.
            //    فمن يترك السكن لا يبقى له به استخدام، وإبقاؤه مفعَّلًا يعني وصولًا
            //    قائمًا إلى شبكة لم يعد من ساكنيها — وهذه ثغرة لا مسألة ترتيب.
            //    الحالات الأربع كلها تُنهي السكن، فكلها تُعطِّل. لا استثناء.

            var (adSuccess, adMessage) = (false, "");
            if (!string.IsNullOrEmpty(student.ad_username))
            {
                try
                {
                    var adUser = await _adService.GetUserBySamAccountNameAsync(student.ad_username);
                    if (adUser != null && adUser.Success && !string.IsNullOrEmpty(adUser.DistinguishedName))
                    {
                        var disableResult = await _adService.DisableUserAsync(adUser.DistinguishedName);
                        if (disableResult.Success)
                        {
                            student.ad_status = AdStatus.disabled;
                            student.ad_last_sync_at = DateTime.UtcNow;
                            (adSuccess, adMessage) = (true, "تم تعطيل حساب الشبكة بنجاح");
                        }
                        else
                        {
                            (adSuccess, adMessage) = (false, "فشل تعطيل حساب الشبكة");
                        }
                    }
                    else
                    {
                        (adSuccess, adMessage) = (false, "لم يتم العثور على حساب الشبكة للطالب");
                    }
                }
                catch (Exception ex)
                {
                    (adSuccess, adMessage) = (false, $"حدث خطأ أثناء التعامل مع الشبكة: {ex.Message}");
                }
            }

            await _lifecycle.AddAsync(new AccountLifecycleLog
            {
                StudentId = student.Id,
                Action = string.IsNullOrEmpty(student.ad_username) ? "left_housing"
                       : adSuccess ? "disabled" : "disable_failed",
                PerformedBy = actorId,
                PerformedAt = DateTime.UtcNow,
                Details = $"{detailsPrefix} - {statusAr}: {notes.Trim()}" +
                          (string.IsNullOrEmpty(student.ad_username)
                              ? " (لا يوجد حساب شبكة)"
                              : adSuccess
                                  ? " (تم تعطيل حساب الشبكة)"
                                  : $" (فشل تعطيل الشبكة: {adMessage})"),
                IpAddress = clientIp
            });

            // حفظ المرفق (لو فيه)
            if (file != null)
            {
                // FileName بقى **مسار نسبي للجذر** مش اسم ملف بس — عشان أي إعادة
                // تنظيم للمجلدات ما تكسرش الصفوف القديمة.
                var storedPath = await _storage.SaveAsync(
                    AttachmentStorage.StatusChange,
                    student.student_id ?? "",
                    "action",
                    action.Id,
                    _http.HttpContext?.User?.Identity?.Name,
                    file);

                await _attachments.AddAsync(new StudentStatusAttachment
                {
                    StudentStatusActionId = action.Id,
                    FileName = storedPath,
                    OriginalFileName = file.FileName,
                    // ⚠️ المشتقّ من الامتداد لا file.ContentType: الأخيرة ترويسة
                    //    يكتبها العميل، وكانت تُخزَّن ثم تُعاد كما هي عند العرض.
                    ContentType = safeContentType ?? AttachmentPolicy.FallbackContentType,
                    FileSize = file.Length,
                    UploadedBy = actorId,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await UnitOfWork.SaveAsync();
            // ⚠️ والحقول القديمة تُسجَّل في سجل العمليات كذلك (AuditChangeLogs):
            //    صفّ سجل حركة التسكين يجيب عن «من سكن في هذه الغرفة»، وهذا
            //    يجيب عن «ما الذي تغيّر في هذا الصفّ بالضبط ومن غيّره».
            var changes = new List<AuditChangeLog>
            {
                new() { FieldName = "student_status", OldValue = previousStatus, NewValue = st }
            };
            if (hadHousing && st != "other")
            {
                changes.Add(new AuditChangeLog { FieldName = "housing_building", OldValue = oldBuilding, NewValue = null });
                changes.Add(new AuditChangeLog { FieldName = "floor_number", OldValue = oldFloor, NewValue = null });
                changes.Add(new AuditChangeLog { FieldName = "apartment_number", OldValue = oldApartment, NewValue = null });
                changes.Add(new AuditChangeLog { FieldName = "room_number", OldValue = oldRoom, NewValue = null });
            }

            await _audit.LogAsync("student_departure_status", "StudentStatusActions", action.Id, changes);

            // فشل التعطيل تحذير لا خطأ: الحالة سُجّلت فعلًا، والحساب يُعطَّل يدويًا
            // من «إدارة حسابات السكن». إخفاء الفشل أسوأ من إظهاره.
            if (!adSuccess && !string.IsNullOrEmpty(student.ad_username))
            {
                return new StudentStatusResultDto
                {
                    Message = $"تم تسجيل حالة {statusAr} للطالب",
                    Warning = adMessage,
                    ActionId = action.Id,
                    StatusType = st,
                    AdDisabled = false,
                    AdError = adMessage
                };
            }

            return new StudentStatusResultDto
            {
                Message = $"تم تسجيل حالة {statusAr} للطالب" + (adSuccess ? " وتم تعطيل حساب الشبكة" : ""),
                ActionId = action.Id,
                StatusType = st,
                AdDisabled = adSuccess,
                AdError = (adSuccess || string.IsNullOrEmpty(student.ad_username))
                    ? null
                    : adMessage
            };
        }

        // الشاشة مابتترسمش قبل ما ده يخلّص، فكل رحلة زايدة للداتابيز بتتحوّل
        // إحساسًا بالبطء عند فتح الصفحة. كان ٦ استعلامات ورا بعض (COUNT لكل
        // حالة)؛ بقوا اتنين: واحد للطلاب وواحد بيجمّع الإجراءات بـ GROUP BY.
        public async Task<StudentStatusStatsDto> GetStatsAsync()
        {
            var scope = UnitOfWork.GetGenderScope();

            // ⚠️ الشرطية اتنقلت لـ Core/GenderScope.cs. كانت مكتوبة بيدها هنا
            //    وفي سجل التنقلات وفي شاشة الطلبات - ثلاث نسخ لقاعدة واحدة،
            //    وأول شاشة تنساها تصير ثغرة صامتة (وهو المكتوب فوق GetGenderScope
            //    نفسها منذ اليوم الأول).
            var studentsQ = _students.Query().AsNoTracking().ForGender(scope).Where(s => !s.IsDeleted);

            var students = await studentsQ
                .GroupBy(s => s.student_status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var actionsQ = _actions.Query().AsNoTracking().ForGender(scope);

            var actions = await actionsQ
                .GroupBy(a => a.StatusType)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int ActionsOf(string s) => actions.Where(a => a.Status == s).Sum(a => a.Count);

            return new StudentStatusStatsDto
            {
                Total = students.Sum(s => s.Count),
                Active = students.Where(s => s.Status == StudentStatus.active).Sum(s => s.Count),
                Graduated = ActionsOf("graduated"),
                Dismissed = ActionsOf("dismissed"),
                Transferred = ActionsOf("transferred"),
                LeftHousing = ActionsOf("left_housing")
            };
        }

        // حجم الصفحة الواحدة من سجل التحديثات. الواجهة تطلب التالية بـ skip.
        public const int RecentPageSize = 50;

        // ⚠️ كانت تُرجع ٥٠ صفًّا وتتوقّف بلا أن تخبر أحدًا: لو في النظام ٢٠٠
        //    إجراء فـ١٥٠ منها غير موجودة في الرد أصلًا، والقائمة على الشاشة
        //    تبدو كاملة وهي ليست كذلك. السقف الصامت أخطر من القائمة الطويلة.
        public async Task<List<RecentStatusActionDto>> GetRecentAsync(int skip = 0)
        {
            // ⚠️ إجراءات النموذج وحدها. ValidStatuses هي عينها قائمة الخيارات التي
            //    يقبلها CreateCoreAsync أعلاه، فالقائمة تعرض ما تنتجه الشاشة
            //    المجاورة لها بالضبط - لا أكثر.
            //
            //    كان يظهر فيها ad_provisioning: صفٌّ يكتبه ADProvisioningService
            //    عند إنشاء حساب الشبكة، لا قرارًا اتّخذه موظف - ولذلك لا سبب له
            //    ولا مرفق. ووجوده في قائمة عنوانها «آخر التحديثات» بجانب نموذج
            //    الحالة الأكاديمية يجعل الموظف يقرؤه كإجراء اتُّخذ على الطالب.
            //    والربط بـ ValidStatuses لا بقائمة استبعاد: أي نوع نظام يُضاف
            //    مستقبلًا يبقى خارج القائمة تلقائيًّا بلا تعديل هنا.
            IQueryable<StudentStatusAction> recentQ = _actions.Query().AsNoTracking()
                .ForGender(UnitOfWork.GetGenderScope())
                .Where(a => a.StatusType != null && ValidStatuses.Contains(a.StatusType))
                .Include(a => a.Student)
                .Include(a => a.CreatedByUser);

            var list = await recentQ
                .OrderByDescending(a => a.CreatedDate)
                .Skip(Math.Max(0, skip))
                .Take(RecentPageSize)
                .Select(a => new RecentStatusActionDto
                {
                    Id = a.Id,
                    StudentNumber = a.StudentNumber,
                    StatusType = a.StatusType,
                    Notes = a.Notes,
                    CreatedDate = a.CreatedDate,
                    CreatedBy = a.CreatedBy,
                    StudentName = a.Student != null ? a.Student.full_name : null,
                    CreatedByName = a.CreatedByUser != null
                        ? (a.CreatedByUser.full_name ?? a.CreatedByUser.UserName)
                        : null
                })
                .ToListAsync();

            // اسم المرفق — كان بيظهر في شاشة المغادرة بس. استعلام واحد لكل الصفوف
            // بدل استعلام لكل صف (N+1).
            var ids = list.Select(a => a.Id).ToList();
            if (ids.Count > 0)
            {
                var attachments = await _attachments.Query().AsNoTracking()
                    .Where(at => ids.Contains(at.StudentStatusActionId) && at.FileName != null)
                    .Select(at => new { at.StudentStatusActionId, at.FileName, at.OriginalFileName })
                    .ToListAsync();

                var map = new Dictionary<int, string>();
                foreach (var at in attachments)
                    if (at.FileName != null && !map.ContainsKey(at.StudentStatusActionId))
                        // ⚠️ المعروض = اسم الملف اللي الموظف رفعه، مش المسار المخزَّن.
                        //    الشاشة كانت بتطبع FileName كامل:
                        //    Students\456320025\Status-Change\2026-08-10_1401__action-20__...
                        //    فالموظف بيقرا مسارًا داخليًا على الخادم بدل «nu-logo.png»،
                        //    وبيتسرّب معاه شكل مجلدات التخزين. شاشة نقل السكن جنبها
                        //    كانت بتعرض الاسم الأصلي صح — دي اللي كانت شاذّة.
                        map[at.StudentStatusActionId] = string.IsNullOrWhiteSpace(at.OriginalFileName)
                            ? Path.GetFileName(at.FileName)
                            : at.OriginalFileName;

                foreach (var a in list)
                    if (map.TryGetValue(a.Id, out var fn))
                        a.AttachmentFileName = fn;
            }

            return list;
        }

        // مرفق الإجراء — بيعدّي من هنا بدل الرابط الثابت في wwwroot، عشان الصلاحية
        // تتفحص الأول (وثائق طلاب)، وعشان الاسم على الديسك GUID مش الاسم الأصلي.
        public async Task<DownloadFileDto> GetActionAttachmentAsync(int actionId)
        {
            // ⚠️ نطاق القسم قبل أي حاجة. الدالة دي كانت بتجيب المرفق برقم
            //    الإجراء وخلاص — ومن غير الفحص ده كان يكفي تخمين رقم عشان
            //    مشرف قسم ينزّل مستند طالبة من القسم التاني (شهادة تخرّج،
            //    خطاب فصل). القاعدة في Core/GenderScope.cs.
            //    «غير موجود» لا «ممنوع» عن قصد: الرد ما يقولش إن السجل موجود
            //    في القسم التاني — نفس صيغة باقي المسارات.
            var inScope = await Scoped(_actions.Query()).AsNoTracking()
                .AnyAsync(a => a.Id == actionId);
            if (!inScope)
                throw UserFriendlyException.NotFound("لا يوجد مرفق لهذا الإجراء");

            var attachment = await _attachments.Query().AsNoTracking()
                .Where(a => a.StudentStatusActionId == actionId)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync()
                ?? throw UserFriendlyException.NotFound("لا يوجد مرفق لهذا الإجراء");

            var filePath = _storage.ResolveExisting(AttachmentStorage.StatusChange, actionId, attachment.FileName)
                ?? throw UserFriendlyException.NotFound("الملف غير موجود على الخادم");

            return new DownloadFileDto
            {
                FilePath = filePath,
                // ⚠️ من مسار الملف لا من عمود ContentType: الصفوف القديمة قد
                //    تحمل نوعًا كاذبًا خزّنه العميل، فاشتقاقه هنا يُصحّحها كلها
                //    بلا ترحيل بيانات.
                ContentType = AttachmentPolicy.ResolveContentType(filePath),
                OriginalFileName = string.IsNullOrWhiteSpace(attachment.OriginalFileName)
                    ? Path.GetFileName(filePath)
                    : attachment.OriginalFileName
            };
        }

        public async Task<List<StudentStatusActionDto>> GetStudentHistoryAsync(int studentId)
        {
            var actions = await Scoped(_actions.Query().AsNoTracking())
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return Mapper.Map<List<StudentStatusActionDto>>(actions);
        }
    }
}
