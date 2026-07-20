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
    [Route("api/[controller]")]
    [ApiController]
    public class RegistrationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RegistrationService _registrationService;
        private readonly WorkflowService _workflowService;
        private readonly ILogger<RegistrationController> _logger;

        public RegistrationController(AppDbContext context, RegistrationService registrationService,
            WorkflowService workflowService, ILogger<RegistrationController> logger)
        {
            _context = context;
            _registrationService = registrationService;
            _workflowService = workflowService;
            _logger = logger;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartRegistration([FromBody] StartRegistrationRequest request)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var student = await _context.Students.FirstOrDefaultAsync(s => s.student_id == request.StudentId);
            if (student == null)
            {
                student = new Student
                {
                    student_id = request.StudentId,
                    full_name = ExtractRegField(request.RegistrationData, "full_name"),
                    full_name_english = ExtractRegField(request.RegistrationData, "full_name_english"),
                    national_id = ExtractRegField(request.RegistrationData, "national_id"),
                    phone = ExtractRegField(request.RegistrationData, "phone") ?? ExtractRegField(request.RegistrationData, "mobile"),
                    gender = ExtractRegField(request.RegistrationData, "gender"),
                    college = ExtractRegField(request.RegistrationData, "college"),
                    department = ExtractRegField(request.RegistrationData, "department"),
                    academic_level = ExtractRegField(request.RegistrationData, "academic_level"),
                    housing_building = ExtractRegField(request.RegistrationData, "housing_building"),
                    room_number = ExtractRegField(request.RegistrationData, "room_number"),
                    apartment_number = ExtractRegField(request.RegistrationData, "apartment_number"),
                    status = ExtractRegField(request.RegistrationData, "status") ?? "active",
                    created_at = DateTime.UtcNow,
                    created_by = actorId
                };
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
            }

            var existingByStudent = await _registrationService.CheckDuplicateByStudentIdAsync(request.StudentId);
            if (existingByStudent)
                return BadRequest(new { message = "لديك طلب تسجيل قيد المراجعة بالفعل" });

            var mobile = ExtractRegField(request.RegistrationData, "mobile")
                         ?? ExtractRegField(request.RegistrationData, "phone");

            if (!string.IsNullOrEmpty(mobile))
            {
                var existingByMobile = await _registrationService.CheckDuplicateByMobileAsync(mobile);
                if (existingByMobile)
                    return BadRequest(new { message = "رقم الجوال مستخدم بالفعل في طلب تسجيل آخر" });
            }

            var requestNumber = await _registrationService.GenerateRequestNumberAsync();
            var registrationDataJson = System.Text.Json.JsonSerializer.Serialize(request.RegistrationData ?? new { });

            var newRequest = await _registrationService.CreateRegistrationRequestAsync(
                student.Id, requestNumber, registrationDataJson, actorId);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(actorId, "registration_created", "Requests", newRequest.Id, ip, ua);

            return Ok(new
            {
                message = "تم تقديم طلب التسجيل بنجاح",
                requestId = newRequest.Id,
                requestNumber = newRequest.RequestNumber
            });
        }

        [HttpPost("{requestId}/declarations")]
        public async Task<IActionResult> AcceptDeclarations(int requestId, [FromBody] AcceptDeclarationsRequest request)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var req = await _context.Requests.FindAsync(requestId);
            if (req == null) return NotFound();

            var declaration = new StudentDeclaration
            {
                RequestId = requestId,
                DeclarationAccepted = request.DeclarationAccepted,
                PolicyAccepted = request.PolicyAccepted,
                PolicyVersion = request.PolicyVersion ?? "1.0",
                AcceptedDate = DateTime.UtcNow,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            };

            _context.StudentDeclarations.Add(declaration);
            await _context.SaveChangesAsync();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(actorId, "declaration_accepted", "StudentDeclarations", declaration.Id, ip, ua);

            return Ok(new { message = "تم قبول الإقرار" });
        }

        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests([FromQuery] string? mobile = null)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<Request> query = _context.Requests
                .Include(r => r.Student)
                .Where(r => r.RequestType == "self_registration");

            if (userRole == "user" || userRole == "student")
            {
                if (!string.IsNullOrEmpty(mobile))
                {
                    var studentIds = await _context.Students
                        .Where(s => s.phone == mobile)
                        .Select(s => s.Id)
                        .ToListAsync();
                    query = query.Where(r => studentIds.Contains(r.StudentId));
                }
                else
                {
                    query = query.Where(r => r.SubmittedBy == actorId);
                }
            }

            var requests = await query
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new
                {
                    r.Id,
                    r.RequestNumber,
                    r.Status,
                    r.SubmittedAt,
                    r.ReviewedAt,
                    StudentName = r.Student!.full_name,
                    StudentId = r.Student.student_id,
                    StudentPhone = r.Student.phone
                })
                .ToListAsync();

            return Ok(requests);
        }

        [HttpGet("my-requests/{requestId}")]
        public async Task<IActionResult> GetMyRequestDetail(int requestId)
        {
            var request = await _context.Requests
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.RequestType == "self_registration");

            if (request == null) return NotFound();

            var history = await _workflowService.GetHistoryAsync(requestId);

            return Ok(new
            {
                request.Id,
                request.RequestNumber,
                request.Status,
                request.RegistrationData,
                request.SubmittedAt,
                request.ReviewedAt,
                request.Notes,
                Student = request.Student == null ? null : new
                {
                    request.Student.student_id,
                    request.Student.full_name,
                    request.Student.national_id,
                    request.Student.college,
                    request.Student.department,
                    request.Student.phone,
                    request.Student.ad_username
                },
                History = history.Select(h => new
                {
                    h.FromStage,
                    h.ToStage,
                    h.ActionDate,
                    h.Notes,
                    ActorName = h.Actor != null
                        ? (h.Actor.role == "user" && request.Student?.full_name != null ? request.Student.full_name : h.Actor.full_name ?? h.Actor.username)
                        : null
                })
            });
        }

        [HttpPost("{requestId}/resubmit")]
        public async Task<IActionResult> ResubmitRequest(int requestId, [FromBody] ResubmitRequest request)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var result = await _registrationService.ResubmitRequestAsync(requestId, actorId, request.RegistrationData);

            if (result == null)
                return BadRequest(new { message = "لا يمكن إعادة تقديم هذا الطلب" });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(actorId, "request_resubmitted", "Requests", requestId, ip, ua);

            return Ok(new { message = "تم إعادة تقديم الطلب" });
        }

        private static string? ExtractRegField(object? regData, string fieldName)
        {
            if (regData == null) return null;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(regData);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(fieldName, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
                    return prop.GetString();
            }
            catch { }
            return null;
        }
    }

    public class StartRegistrationRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public object? RegistrationData { get; set; }
    }

    public class AcceptDeclarationsRequest
    {
        public bool DeclarationAccepted { get; set; }
        public bool PolicyAccepted { get; set; }
        public string? PolicyVersion { get; set; }
    }

    public class ResubmitRequest
    {
        public string? RegistrationData { get; set; }
    }
}
