using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.Text.RegularExpressions;

namespace NUH_PORTAL.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private static readonly HashSet<string> ValidBuildings = new() { "40", "41", "42", "43", "65", "66", "67", "68", "69", "70" };

        public StudentsController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudents(
            [FromQuery] bool showDeleted = false,
            [FromQuery] string? adStatus = null)
        {
            var query = _context.Students.AsQueryable();
            if (!showDeleted)
                query = query.Where(s => !s.IsDeleted);
            if (!string.IsNullOrEmpty(adStatus))
            {
                query = adStatus.ToLower() switch
                {
                    "enabled" => query.Where(s => s.ad_status == "enabled"),
                    "disabled" => query.Where(s => s.ad_status == "disabled"),
                    "none" => query.Where(s => s.ad_status == null || s.ad_status == ""),
                    "any" => query.Where(s => s.ad_status != null && s.ad_status != ""),
                    _ => query
                };
            }
            return await query.ToListAsync();
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = new
            {
                total = await _context.Students.CountAsync(s => !s.IsDeleted),
                active = await _context.Students.CountAsync(s => (s.status == "active" || s.status == "Active") && !s.IsDeleted),
                left = await _context.Students.CountAsync(s => (s.status == "left" || s.status == "Left") && !s.IsDeleted),
                submitted = await _context.Requests.CountAsync(r => r.Status == "submitted"),
                housing_approved = await _context.Requests.CountAsync(r => r.Status == "housing_approved"),
                housing_rejected = await _context.Requests.CountAsync(r => r.Status == "housing_rejected"),
                cyber_review = await _context.Requests.CountAsync(r => r.Status == "cyber_review"),
                cyber_approved = await _context.Requests.CountAsync(r => r.Status == "cyber_approved"),
                cyber_rejected = await _context.Requests.CountAsync(r => r.Status == "cyber_rejected"),
                ready_for_provisioning = await _context.Requests.CountAsync(r => r.Status == "ready_for_provisioning"),
                completed = await _context.Requests.CountAsync(r => r.Status == "completed"),
            };
            return Ok(stats);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Student>> GetStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            return student;
        }

        private List<string> ValidateStudent(Student student)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(student.full_name) || !Regex.IsMatch(student.full_name, @"^[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\s]+$"))
                errors.Add("الاسم بالعربية: يرجى إدخال الاسم باللغة العربية فقط");
            if (string.IsNullOrEmpty(student.full_name_english) || !Regex.IsMatch(student.full_name_english, @"^[a-zA-Z\s]+$"))
                errors.Add("الاسم بالإنجليزية: يرجى إدخال الاسم باللغة الإنجليزية فقط");
            if (string.IsNullOrEmpty(student.student_id) || !Regex.IsMatch(student.student_id, @"^\d{9,10}$"))
                errors.Add("الرقم الجامعي: يجب أن يتكون الرقم الجامعي من 9 أو 10 أرقام");
            if (string.IsNullOrEmpty(student.national_id) || !Regex.IsMatch(student.national_id, @"^\d{10}$"))
                errors.Add("رقم الهوية: يجب أن يتكون رقم الهوية من 10 أرقام");
            if (!string.IsNullOrEmpty(student.phone) && !Regex.IsMatch(student.phone, @"^9665\d{8}$"))
                errors.Add("رقم الجوال: يجب أن يبدأ الرقم بـ 9665 ويتكون من 12 رقمًا");
            if (!string.IsNullOrEmpty(student.housing_building) && !ValidBuildings.Contains(student.housing_building))
                errors.Add("رقم المبنى السكني غير صحيح - القيم المسموح بها: 40,41,42,43,65,66,67,68,69,70");
            return errors;
        }

        [HttpPost]
        public async Task<ActionResult<Student>> CreateStudent([FromBody] Student student)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower();
            if (role == "user")
                return Forbid();

            try
            {
                if (student == null)
                    return BadRequest(new { message = "البيانات مطلوبة" });

                var errors = ValidateStudent(student);
                if (await _context.Students.AnyAsync(s => s.student_id == student.student_id && !s.IsDeleted))
                    errors.Add("الرقم الجامعي موجود بالفعل");
                if (await _context.Students.AnyAsync(s => s.national_id == student.national_id && !s.IsDeleted))
                    errors.Add("رقم الهوية موجود بالفعل");
                if (errors.Count > 0)
                    return BadRequest(new { message = string.Join(" | ", errors) });

                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                student.created_at = DateTime.UtcNow;
                student.status = string.IsNullOrEmpty(student.status) ? "active" : student.status;
                student.student_status = string.IsNullOrEmpty(student.student_status) ? "active" : student.student_status;
                student.created_by = int.TryParse(userIdClaim, out var uid) ? uid : 0;
                if (!string.IsNullOrEmpty(student.gender))
                    student.gender = GenderHelper.NormalizeSafely(student.gender);

                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var aid) ? aid : 0;
                if (actorId > 0)
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        user_id = actorId,
                        action = "create_student",
                        target_table = "Students",
                        target_id = student.Id,
                        action_at = DateTime.UtcNow,
                        ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        user_agent = Request.Headers["User-Agent"].ToString()
                    });
                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction(nameof(GetStudent), new { id = student.Id }, student);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
            {
                Console.Error.WriteLine($"[StudentsController.CreateStudent] UNIQUE CONSTRAINT: {sqlEx.Message}");
                return Conflict(new { message = "بيانات مكررة: رقم الهوية أو الرقم الجامعي موجود بالفعل" });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[StudentsController.CreateStudent] ERROR: {ex.GetType().FullName}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"[StudentsController.CreateStudent] INNER: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                Console.Error.WriteLine($"[StudentsController.CreateStudent] STACK: {ex.StackTrace}");
                return StatusCode(500, new { message = "حدث خطأ، يرجى المحاولة لاحقاً" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] Student updated)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower();
            if (role == "user")
                return Forbid();

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            var errors = ValidateStudent(updated);
            if (errors.Count > 0)
                return BadRequest(new { message = string.Join(" | ", errors) });

            var changes = new List<AuditChangeLog>();
            var fields = new (string field, string? oldVal, string? newVal)[]
            {
                ("full_name", student.full_name, updated.full_name),
                ("full_name_english", student.full_name_english, updated.full_name_english),
                ("national_id", student.national_id, updated.national_id),
                ("phone", student.phone, updated.phone),
                ("gender", student.gender, updated.gender),
                ("college", student.college, updated.college),
                ("department", student.department, updated.department),
                ("academic_level", student.academic_level, updated.academic_level),
                ("housing_building", student.housing_building, updated.housing_building),
                ("room_number", student.room_number, updated.room_number),
                ("apartment_number", student.apartment_number, updated.apartment_number),
                ("status", student.status, updated.status),
            };

            bool modified = false;
            foreach (var (field, oldVal, newVal) in fields)
            {
                if (!string.IsNullOrEmpty(newVal) && oldVal != newVal)
                {
                    if (field == "full_name" && !string.IsNullOrEmpty(updated.full_name)) student.full_name = updated.full_name;
                    else if (field == "full_name_english") student.full_name_english = updated.full_name_english;
                    else if (field == "national_id" && !string.IsNullOrEmpty(updated.national_id)) student.national_id = updated.national_id;
                    else if (field == "phone" && !string.IsNullOrEmpty(updated.phone)) student.phone = updated.phone;
                    else if (field == "gender" && !string.IsNullOrEmpty(updated.gender)) student.gender = GenderHelper.NormalizeSafely(updated.gender);
                    else if (field == "college" && !string.IsNullOrEmpty(updated.college)) student.college = updated.college;
                    else if (field == "department") student.department = updated.department;
                    else if (field == "academic_level") student.academic_level = updated.academic_level;
                    else if (field == "housing_building" && !string.IsNullOrEmpty(updated.housing_building)) student.housing_building = updated.housing_building;
                    else if (field == "room_number" && !string.IsNullOrEmpty(updated.room_number)) student.room_number = updated.room_number;
                    else if (field == "apartment_number") student.apartment_number = updated.apartment_number;
                    else if (field == "status" && !string.IsNullOrEmpty(updated.status)) student.status = updated.status;

                    changes.Add(new AuditChangeLog
                    {
                        FieldName = field,
                        OldValue = oldVal,
                        NewValue = newVal
                    });
                    modified = true;
                }
            }

            if (!modified)
                return Ok(student);

            await _context.SaveChangesAsync();

            var uActorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uAid) ? uAid : 0;
            if (uActorId > 0)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    user_id = uActorId,
                    action = "update_student",
                    target_table = "Students",
                    target_id = student.Id,
                    action_at = DateTime.UtcNow,
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    AuditChangeLogs = changes
                });
                await _context.SaveChangesAsync();
            }

            return Ok(student);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteStudent(int id)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower();
            if (role != "admin")
                return StatusCode(403, new { message = "غير مسموح لك بحذف الطالب. يرجى التواصل مع مسؤول النظام.", messageEn = "You do not have permission to delete students. Please contact your administrator." });

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            if (student.IsDeleted)
                return BadRequest(new { message = "الطالب محذوف بالفعل", messageEn = "Student is already deleted" });

            var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var aid) ? aid : 0;

            student.IsDeleted = true;
            student.DeletedBy = actorId > 0 ? actorId : null;
            student.DeletedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (actorId > 0)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    user_id = actorId,
                    action = "soft_delete_student",
                    target_table = "Students",
                    target_id = id,
                    action_at = DateTime.UtcNow,
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    user_agent = Request.Headers["User-Agent"].ToString()
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "تم حذف الطالب بنجاح", messageEn = "Student deleted successfully", deleted = true });
        }

        [HttpGet("{id}/lifecycle")]
        public async Task<IActionResult> GetLifecycleLogs(int id)
        {
            var logs = await _context.AccountLifecycleLogs
                .Where(l => l.StudentId == id)
                .OrderByDescending(l => l.PerformedAt)
                .Take(50)
                .Select(l => new
                {
                    l.Id,
                    l.Action,
                    l.PerformedAt,
                    l.Details,
                    l.IpAddress,
                    PerformerName = l.Performer != null ? l.Performer.full_name : ""
                })
                .ToListAsync();

            return Ok(new { logs });
        }

        [HttpPost("{id}/restore")]
        public async Task<IActionResult> RestoreStudent(int id)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower();
            if (role != "admin")
                return StatusCode(403, new { message = "غير مسموح لك باستعادة الطالب. يرجى التواصل مع مسؤول النظام.", messageEn = "You do not have permission to restore students. Please contact your administrator." });

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            if (!student.IsDeleted)
                return BadRequest(new { message = "الطالب غير محذوف", messageEn = "Student is not deleted" });

            var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var aid) ? aid : 0;

            student.IsDeleted = false;
            student.RestoredBy = actorId > 0 ? actorId : null;
            student.RestoredDate = DateTime.UtcNow;
            student.DeletedBy = null;
            student.DeletedDate = null;

            await _context.SaveChangesAsync();

            if (actorId > 0)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    user_id = actorId,
                    action = "restore_student",
                    target_table = "Students",
                    target_id = id,
                    action_at = DateTime.UtcNow,
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    user_agent = Request.Headers["User-Agent"].ToString()
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "تم استعادة الطالب بنجاح", messageEn = "Student restored successfully", restored = true });
        }
    }
}