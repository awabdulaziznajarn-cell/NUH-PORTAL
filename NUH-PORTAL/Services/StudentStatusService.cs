using MapsterMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.StudentStatus;
using NUH_PORTAL.Models;
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
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _http;
        private readonly IAuditService _audit;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".docx"
        };

        public const long MaxFileSize = 10 * 1024 * 1024;

        private static readonly string[] ValidStatuses = { "graduated", "dismissed", "transferred", "left_housing" };

        public StudentStatusService(
            IRepository<Student> students,
            IRepository<StudentStatusAction> actions,
            IRepository<AccountLifecycleLog> lifecycle,
            IRepository<StudentStatusAttachment> attachments,
            ActiveDirectoryService adService,
            IWebHostEnvironment env,
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
            _env = env;
            _http = http;
            _audit = audit;
        }

        public async Task<StudentStatusResultDto> CreateStatusActionAsync(string studentNumber, string statusType, string notes, IFormFile? file)
        {
            var role = UnitOfWork.GetCurrentUserRole()?.ToLower();
            if (role == "user" || role == "cyber")
                throw UserFriendlyException.Forbidden();

            return await CreateCoreAsync(studentNumber, statusType, notes, file,
                detailsPrefix: "Admin status change",
                invalidStatusMessage: "نوع الحالة غير صحيح - القيم المسموح بها: تخرج, فصل من الكلية, تحويل إلى جامعة أخرى, ترك الإسكان الجامعي");
        }

        public async Task<StudentStatusResultDto> CreateSupervisorStatusActionAsync(string studentNumber, string statusType, string notes, IFormFile? file)
        {
            if (UnitOfWork.GetCurrentUserRole()?.ToLower() != "supervisor")
                throw UserFriendlyException.Forbidden();

            return await CreateCoreAsync(studentNumber, statusType, notes, file,
                detailsPrefix: "Supervisor departure",
                invalidStatusMessage: "نوع الحالة غير صحيح");
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

            student.student_status = st;
            student.status = "left";
            if (st == "left_housing")
            {
                student.housing_building = null;
                student.room_number = null;
                student.apartment_number = null;
            }

            await _actions.AddAsync(action);
            await UnitOfWork.SaveAsync();

            // محاولة تعطيل حساب الشبكة (لو موجود)
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
                            student.ad_status = "disabled";
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
                Action = adSuccess ? "disabled" : "disable_failed",
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
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "student-status", action.Id.ToString());
                Directory.CreateDirectory(uploadDir);

                var ext = Path.GetExtension(file.FileName);
                var storedFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadDir, storedFileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                await _attachments.AddAsync(new StudentStatusAttachment
                {
                    StudentStatusActionId = action.Id,
                    FileName = storedFileName,
                    OriginalFileName = file.FileName,
                    ContentType = file.ContentType ?? "application/octet-stream",
                    FileSize = file.Length,
                    UploadedBy = actorId,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("student_departure_status", "StudentStatusActions", action.Id);

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
                AdError = adSuccess ? null : (string.IsNullOrEmpty(student.ad_username) ? null : adMessage)
            };
        }

        public async Task<StudentStatusStatsDto> GetStatsAsync()
        {
            return new StudentStatusStatsDto
            {
                Total = await _students.Query().AsNoTracking().CountAsync(s => !s.IsDeleted),
                Active = await _students.Query().AsNoTracking().CountAsync(s => s.student_status == "active" && !s.IsDeleted),
                Graduated = await _actions.Query().AsNoTracking().CountAsync(a => a.StatusType == "graduated"),
                Dismissed = await _actions.Query().AsNoTracking().CountAsync(a => a.StatusType == "dismissed"),
                Transferred = await _actions.Query().AsNoTracking().CountAsync(a => a.StatusType == "transferred"),
                LeftHousing = await _actions.Query().AsNoTracking().CountAsync(a => a.StatusType == "left_housing")
            };
        }

        public async Task<List<RecentStatusActionDto>> GetRecentAsync()
        {
            return await _actions.Query().AsNoTracking()
                .Include(a => a.Student)
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
                    StudentName = a.Student != null ? a.Student.full_name : null
                })
                .ToListAsync();
        }

        public async Task<List<StudentStatusActionDto>> GetStudentHistoryAsync(int studentId)
        {
            var actions = await _actions.Query().AsNoTracking()
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return Mapper.Map<List<StudentStatusActionDto>>(actions);
        }

        public async Task<List<SupervisorRecentActionDto>> GetRecentForSupervisorAsync()
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            // ملحوظة: ضفنا Include(Student) — الكود القديم كان بيرجّع StudentName = null دايمًا (باج)
            var actions = await _actions.Query().AsNoTracking()
                .Include(a => a.Student)
                .Where(a => a.CreatedBy == actorId)
                .OrderByDescending(a => a.CreatedDate)
                .Take(50)
                .ToListAsync();

            var actionIds = actions.Select(a => a.Id).ToList();
            var attachments = await _attachments.Query().AsNoTracking()
                .Where(at => actionIds.Contains(at.StudentStatusActionId))
                .ToListAsync();

            var attachmentMap = new Dictionary<int, string>();
            foreach (var at in attachments)
            {
                if (at.FileName != null && !attachmentMap.ContainsKey(at.StudentStatusActionId))
                    attachmentMap[at.StudentStatusActionId] = at.FileName;
            }

            return actions.Select(a => new SupervisorRecentActionDto
            {
                Id = a.Id,
                StudentNumber = a.StudentNumber,
                StatusType = a.StatusType,
                Notes = a.Notes,
                CreatedDate = a.CreatedDate,
                StudentName = a.Student != null ? a.Student.full_name : null,
                AttachmentFileName = attachmentMap.TryGetValue(a.Id, out var fn) ? fn : null
            }).ToList();
        }
    }
}
