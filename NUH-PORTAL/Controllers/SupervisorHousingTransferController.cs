using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    [Authorize(Roles = "supervisor")]
    [Route("api/supervisor/housing-transfer")]
    [ApiController]
    public class SupervisorHousingTransferController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".docx"
        };

        private const long MaxFileSize = 10 * 1024 * 1024;

        public SupervisorHousingTransferController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpPost]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> CreateTransfer(
            [FromForm] string studentNumber,
            [FromForm] string newBuilding,
            [FromForm] string newApartment,
            [FromForm] string newRoom,
            [FromForm] string reason,
            [FromForm] string? customReason,
            IFormFile? file)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
            if (role != "supervisor")
                return Forbid();

            if (string.IsNullOrEmpty(studentNumber))
                return BadRequest(new { message = "الرقم الجامعي مطلوب" });

            if (string.IsNullOrEmpty(newBuilding))
                return BadRequest(new { message = "رقم المبنى الجديد مطلوب" });

            if (string.IsNullOrEmpty(newApartment))
                return BadRequest(new { message = "رقم الشقة الجديدة مطلوب" });

            if (string.IsNullOrEmpty(newRoom))
                return BadRequest(new { message = "رقم الغرفة الجديدة مطلوب" });

            if (string.IsNullOrEmpty(reason))
                return BadRequest(new { message = "سبب النقل مطلوب" });

            if (reason == "other" && string.IsNullOrEmpty(customReason))
                return BadRequest(new { message = "يرجى كتابة وصف السبب" });

            var student = await _context.Students.FirstOrDefaultAsync(s => s.student_id == studentNumber && !s.IsDeleted);
            if (student == null)
                return BadRequest(new { message = "الطالب غير موجود" });

            if (string.IsNullOrEmpty(student.housing_building) && string.IsNullOrEmpty(student.room_number) && string.IsNullOrEmpty(student.apartment_number))
                return BadRequest(new { message = "الطالب لا يمتلك بيانات سكن حالية" });

            var oldBuilding = student.housing_building ?? "";
            var oldApartment = student.apartment_number ?? "";
            var oldRoom = student.room_number ?? "";

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

            var transfer = new HousingTransfer
            {
                StudentId = student.Id,
                StudentNumber = student.student_id,
                OldBuilding = oldBuilding,
                OldApartment = oldApartment,
                OldRoom = oldRoom,
                NewBuilding = newBuilding,
                NewApartment = newApartment,
                NewRoom = newRoom,
                Reason = reason,
                CustomReason = reason == "other" ? customReason : null,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            };

            _context.HousingTransfers.Add(transfer);
            await _context.SaveChangesAsync();

            student.housing_building = newBuilding;
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

            _context.AccountLifecycleLogs.Add(new AccountLifecycleLog
            {
                StudentId = student.Id,
                Action = "housing_transfer",
                PerformedBy = actorId,
                PerformedAt = DateTime.UtcNow,
                Details = $"نقل سكن طالب: {oldBuilding}/{oldApartment}/{oldRoom} -> {newBuilding}/{newApartment}/{newRoom} - السبب: {reasonAr}",
                IpAddress = clientIp
            });

            _context.AuditLogs.Add(new AuditLog
            {
                user_id = actorId,
                action = "housing_transfer",
                target_table = "HousingTransfers",
                target_id = transfer.Id,
                action_at = DateTime.UtcNow,
                ip_address = clientIp,
                user_agent = userAgent
            });

            string? storedFileName = null;
            if (file != null)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "housing-transfer", transfer.Id.ToString());
                Directory.CreateDirectory(uploadDir);

                var ext = Path.GetExtension(file.FileName);
                storedFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadDir, storedFileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                transfer.AttachmentPath = storedFileName;
                transfer.OriginalFileName = file.FileName;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "تم نقل الطالب بنجاح",
                transferId = transfer.Id,
                oldLocation = $"{oldBuilding}/{oldApartment}/{oldRoom}",
                newLocation = $"{newBuilding}/{newApartment}/{newRoom}"
            });
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
        {
            var actorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(actorIdClaim, out var actorId))
                return Unauthorized();

            var transfers = await _context.HousingTransfers
                .Where(t => t.CreatedBy == actorId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(transfers.Select(t => new
            {
                t.Id,
                t.StudentNumber,
                t.OldBuilding,
                t.OldApartment,
                t.OldRoom,
                t.NewBuilding,
                t.NewApartment,
                t.NewRoom,
                t.Reason,
                t.CustomReason,
                t.CreatedAt,
                t.CreatedBy,
                t.AttachmentPath,
                t.OriginalFileName,
                StudentName = t.Student != null ? t.Student.full_name : null
            }));
        }
    }
}
