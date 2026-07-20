using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    [Authorize]
    [Route("api/student-status")]
    [ApiController]
    public class StudentStatusController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActiveDirectoryService _adService;
        private readonly IWebHostEnvironment _env;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".docx"
        };

        private const long MaxFileSize = 10 * 1024 * 1024;

        public StudentStatusController(AppDbContext context, ActiveDirectoryService adService, IWebHostEnvironment env)
        {
            _context = context;
            _adService = adService;
            _env = env;
        }

        [HttpPost]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> CreateStatusAction(
            [FromForm] string studentNumber,
            [FromForm] string statusType,
            [FromForm] string notes,
            IFormFile? file)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
            if (role == "user" || role == "cyber")
                return Forbid();

            if (string.IsNullOrEmpty(studentNumber))
                return BadRequest(new { message = "الرقم الجامعي مطلوب" });

            if (string.IsNullOrEmpty(notes) || notes.Trim().Length < 5)
                return BadRequest(new { message = "سبب الإجراء إلزامي (5 أحرف على الأقل)" });

            var student = await _context.Students.FirstOrDefaultAsync(s => s.student_id == studentNumber && !s.IsDeleted);
            if (student == null)
                return BadRequest(new { message = "الطالب غير موجود" });

            var validStatuses = new[] { "graduated", "dismissed", "transferred", "left_housing" };
            var st = statusType?.Trim().ToLower();
            if (string.IsNullOrEmpty(st) || !validStatuses.Contains(st))
                return BadRequest(new { message = "نوع الحالة غير صحيح - القيم المسموح بها: تخرج, فصل من الكلية, تحويل إلى جامعة أخرى, ترك الإسكان الجامعي" });

            if (file != null)
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedTypes.Contains(ext))
                    return BadRequest(new { message = $"نوع الملف {ext} غير مسموح به. الصيغ المسموحة: PDF, JPG, JPEG, PNG, DOCX" });
                if (file.Length > MaxFileSize)
                    return BadRequest(new { message = "حجم الملف يتجاوز 10 ميجابايت" });
            }

            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0)
                return Unauthorized();

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

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

            _context.StudentStatusActions.Add(action);
            await _context.SaveChangesAsync();

            var adResult = new { Success = false, Message = "", Verified = false };
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

                            adResult = new { Success = true, Message = "تم تعطيل حساب الشبكة بنجاح", Verified = true };
                        }
                        else
                        {
                            adResult = new { Success = false, Message = "فشل تعطيل حساب الشبكة", Verified = false };
                        }
                    }
                    else
                    {
                        adResult = new { Success = false, Message = "لم يتم العثور على حساب الشبكة للطالب", Verified = false };
                    }
                }
                catch (Exception ex)
                {
                    adResult = new { Success = false, Message = $"حدث خطأ أثناء التعامل مع الشبكة: {ex.Message}", Verified = false };
                }
            }

            _context.AccountLifecycleLogs.Add(new AccountLifecycleLog
            {
                StudentId = student.Id,
                Action = adResult.Success ? "disabled" : "disable_failed",
                PerformedBy = actorId,
                PerformedAt = DateTime.UtcNow,
                Details = $"Admin status change - {statusAr}: {notes.Trim()}" +
                          (string.IsNullOrEmpty(student.ad_username)
                              ? " (لا يوجد حساب شبكة)"
                              : adResult.Success
                                  ? " (تم تعطيل حساب الشبكة)"
                                  : $" (فشل تعطيل الشبكة: {adResult.Message})"),
                IpAddress = clientIp
            });

            _context.AuditLogs.Add(new AuditLog
            {
                user_id = actorId,
                action = "student_departure_status",
                target_table = "StudentStatusActions",
                target_id = action.Id,
                action_at = DateTime.UtcNow,
                ip_address = clientIp,
                user_agent = userAgent
            });

            string? storedFileName = null;
            if (file != null)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "student-status", action.Id.ToString());
                Directory.CreateDirectory(uploadDir);

                var ext = Path.GetExtension(file.FileName);
                storedFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadDir, storedFileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _context.StudentStatusAttachments.Add(new StudentStatusAttachment
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

            await _context.SaveChangesAsync();

            if (!adResult.Success && !string.IsNullOrEmpty(student.ad_username))
            {
                return Ok(new
                {
                    message = $"تم تسجيل حالة {statusAr} للطالب",
                    warning = adResult.Message,
                    actionId = action.Id,
                    statusType = st,
                    adDisabled = false,
                    adError = adResult.Message
                });
            }

            return Ok(new
            {
                message = $"تم تسجيل حالة {statusAr} للطالب" +
                          (adResult.Success ? " وتم تعطيل حساب الشبكة" : ""),
                actionId = action.Id,
                statusType = st,
                adDisabled = adResult.Success,
                adError = adResult.Success ? null : adResult.Message
            });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var total = await _context.Students.CountAsync(s => !s.IsDeleted);
            var active = await _context.Students.CountAsync(s => s.student_status == "active" && !s.IsDeleted);
            var graduated = await _context.StudentStatusActions.CountAsync(a => a.StatusType == "graduated");
            var dismissed = await _context.StudentStatusActions.CountAsync(a => a.StatusType == "dismissed");
            var transferred = await _context.StudentStatusActions.CountAsync(a => a.StatusType == "transferred");
            var leftHousing = await _context.StudentStatusActions.CountAsync(a => a.StatusType == "left_housing");

            return Ok(new { total, active, graduated, dismissed, transferred, leftHousing });
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
        {
            var actions = await _context.StudentStatusActions
                .Include(a => a.Student)
                .OrderByDescending(a => a.CreatedDate)
                .Take(50)
                .ToListAsync();

            return Ok(actions.Select(a => new
            {
                a.Id,
                a.StudentNumber,
                a.StatusType,
                a.Notes,
                a.CreatedDate,
                a.CreatedBy,
                StudentName = a.Student?.full_name
            }));
        }

        [HttpGet("{studentId}")]
        public async Task<IActionResult> GetStudentHistory(int studentId)
        {
            var actions = await _context.StudentStatusActions
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return Ok(actions);
        }
    }
}
