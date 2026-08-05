using MapsterMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        private readonly ActiveDirectoryService _adService;
        private readonly IAttachmentStorage _storage;
        private readonly IHttpContextAccessor _http;
        private readonly IAuditService _audit;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".docx"
        };

        public const long MaxFileSize = 10 * 1024 * 1024;

        private static readonly string[] ValidStatuses = { "graduated", "dismissed", "transferred", "left_housing" };

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
            _ => st?.ToString() ?? ""
        };

        public StudentStatusService(
            IRepository<Student> students,
            IRepository<StudentStatusAction> actions,
            IRepository<AccountLifecycleLog> lifecycle,
            IRepository<StudentStatusAttachment> attachments,
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

            var student = await _students.FindAsync(s => s.student_id == studentNumber && !s.IsDeleted)
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
                    $"حالة الطالب مسجّلة بالفعل: {currentAr}. لا يمكن تسجيل حالة نهائية جديدة — راجع مسؤول النظام لتصحيحها.", 400);
            }

            if (file != null)
            {
                var fileExt = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(fileExt) || !AllowedTypes.Contains(fileExt))
                    throw new UserFriendlyException($"نوع الملف {fileExt} غير مسموح به. الصيغ المسموحة: PDF, JPG, JPEG, PNG, DOCX", 400);
                if (file.Length > MaxFileSize)
                    throw new UserFriendlyException("حجم الملف يتجاوز 10 ميجابايت", 400);
            }

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

            student.student_status = st switch
            {
                "graduated" => StudentStatus.graduated,
                "dismissed" => StudentStatus.dismissed,
                "transferred" => StudentStatus.transferred,
                "left_housing" => StudentStatus.left_housing,
                _ => student.student_status
            };
            student.status = StudentState.left;
            if (st == "left_housing")
            {
                student.housing_building = null;
                student.room_number = null;
                student.apartment_number = null;
            }

            await _actions.AddAsync(action);
            await UnitOfWork.SaveAsync();

            // ⚠️ «ترك الإسكان الجامعي» مايتعملهاش تعطيل.
            //    الطالب ساب السكن بس — لسه طالب في الجامعة ومحتاج حسابه للدراسة.
            //    التعطيل للحالات اللي بتنهي علاقته بالجامعة: تخرّج / فصل / تحويل.
            //    قبل كده كانت كل الحالات بتعطّل، يعني طالب بينتقل لسكن خارجي
            //    كان بيفقد حسابه الجامعي.
            var disablesNetworkAccount = st is "graduated" or "dismissed" or "transferred";

            var (adSuccess, adMessage) = (false, "");
            if (disablesNetworkAccount && !string.IsNullOrEmpty(student.ad_username))
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
                Action = !disablesNetworkAccount ? "left_housing"
                       : adSuccess ? "disabled" : "disable_failed",
                PerformedBy = actorId,
                PerformedAt = DateTime.UtcNow,
                Details = $"{detailsPrefix} - {statusAr}: {notes.Trim()}" +
                          (!disablesNetworkAccount
                              ? " (حساب الشبكة لم يُمسّ — ترك السكن لا ينهي العلاقة بالجامعة)"
                              : string.IsNullOrEmpty(student.ad_username)
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
                    ContentType = file.ContentType ?? "application/octet-stream",
                    FileSize = file.Length,
                    UploadedBy = actorId,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("student_departure_status", "StudentStatusActions", action.Id);

            // التحذير للحالات اللي المفروض تعطّل بس — «ترك السكن» نجاح كامل من غير تعطيل
            if (disablesNetworkAccount && !adSuccess && !string.IsNullOrEmpty(student.ad_username))
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
                AdError = (adSuccess || !disablesNetworkAccount || string.IsNullOrEmpty(student.ad_username))
                    ? null
                    : adMessage
            };
        }

        // الشاشة مابتترسمش قبل ما ده يخلّص، فكل رحلة زايدة للداتابيز بتتحوّل
        // إحساسًا بالبطء عند فتح الصفحة. كان ٦ استعلامات ورا بعض (COUNT لكل
        // حالة)؛ بقوا اتنين: واحد للطلاب وواحد بيجمّع الإجراءات بـ GROUP BY.
        public async Task<StudentStatusStatsDto> GetStatsAsync()
        {
            var students = await _students.Query().AsNoTracking()
                .Where(s => !s.IsDeleted)
                .GroupBy(s => s.student_status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var actions = await _actions.Query().AsNoTracking()
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

        public async Task<List<RecentStatusActionDto>> GetRecentAsync()
        {
            var list = await _actions.Query().AsNoTracking()
                .Include(a => a.Student)
                .Include(a => a.CreatedByUser)
                .OrderByDescending(a => a.CreatedDate)
                .Take(50)
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
                    .Select(at => new { at.StudentStatusActionId, at.FileName })
                    .ToListAsync();

                var map = new Dictionary<int, string>();
                foreach (var at in attachments)
                    if (at.FileName != null && !map.ContainsKey(at.StudentStatusActionId))
                        map[at.StudentStatusActionId] = at.FileName;

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
                ContentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType,
                OriginalFileName = string.IsNullOrWhiteSpace(attachment.OriginalFileName)
                    ? Path.GetFileName(filePath)
                    : attachment.OriginalFileName
            };
        }

        public async Task<List<StudentStatusActionDto>> GetStudentHistoryAsync(int studentId)
        {
            var actions = await _actions.Query().AsNoTracking()
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return Mapper.Map<List<StudentStatusActionDto>>(actions);
        }
    }
}
