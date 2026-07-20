using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class RequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ADProvisioningService _adProvisioning;
        private readonly ILogger<RequestsController> _logger;
        public RequestsController(AppDbContext context, ADProvisioningService adProvisioning, ILogger<RequestsController> logger)
        {
            _context = context;
            _adProvisioning = adProvisioning;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Request>>> GetRequests()
            => await _context.Requests.Include(r => r.Student).ToListAsync();

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetRequest(int id)
        {
            var request = await _context.Requests.Include(r => r.Student).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var userIds = new HashSet<int>();
            if (request.SubmittedBy.HasValue) userIds.Add(request.SubmittedBy.Value);
            if (request.HousingReviewedBy.HasValue) userIds.Add(request.HousingReviewedBy.Value);
            if (request.CyberReviewedBy.HasValue) userIds.Add(request.CyberReviewedBy.Value);
            if (request.ReadyForProvisioningBy.HasValue) userIds.Add(request.ReadyForProvisioningBy.Value);
            if (request.CompletedBy.HasValue) userIds.Add(request.CompletedBy.Value);

            var userNames = new Dictionary<int, string>();
            if (userIds.Count > 0)
            {
                userNames = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.full_name ?? u.username ?? "Unknown");
            }

            return new {
                request.Id, request.RequestNumber, request.RequestType, request.StudentId, request.Status, request.Notes,
                request.SubmittedAt, request.ReviewedAt, request.ReviewedBy,
                request.HousingReviewedAt, request.HousingReviewedBy, request.HousingNotes,
                request.CyberReviewedAt, request.CyberReviewedBy, request.CyberNotes,
                request.ReadyForProvisioningAt, request.ReadyForProvisioningBy,
                request.CompletedAt, request.CompletedBy, request.BulkRequestId,
                request.RequestedByRole,
                Student = request.Student,
                SubmittedByName = request.SubmittedBy.HasValue && userNames.ContainsKey(request.SubmittedBy.Value) ? userNames[request.SubmittedBy.Value] : null,
                HousingReviewedByName = request.HousingReviewedBy.HasValue && userNames.ContainsKey(request.HousingReviewedBy.Value) ? userNames[request.HousingReviewedBy.Value] : null,
                CyberReviewedByName = request.CyberReviewedBy.HasValue && userNames.ContainsKey(request.CyberReviewedBy.Value) ? userNames[request.CyberReviewedBy.Value] : null,
                ReadyForProvisioningByName = request.ReadyForProvisioningBy.HasValue && userNames.ContainsKey(request.ReadyForProvisioningBy.Value) ? userNames[request.ReadyForProvisioningBy.Value] : null,
                CompletedByName = request.CompletedBy.HasValue && userNames.ContainsKey(request.CompletedBy.Value) ? userNames[request.CompletedBy.Value] : null
            };
        }

        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<Request>>> GetPending()
            => await _context.Requests.Where(r => r.Status == "submitted").ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Request>> CreateRequest(Request request)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
            if (role == "user")
                return Forbid();

            if (request == null) return BadRequest();

            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

            // Auto-approve housing AND immediately move to cyber review when supervisor or admin creates the request
            var isHousingCreator = role == "supervisor" || role == "admin";
            request.Status = isHousingCreator ? "cyber_review" : "submitted";
            request.SubmittedBy = actorId > 0 ? actorId : null;
            request.SubmittedAt = DateTime.UtcNow;
            request.RequestedByRole = role;

            if (isHousingCreator)
            {
                request.HousingReviewedBy = actorId;
                request.HousingReviewedAt = DateTime.UtcNow;
                request.ReviewedBy = actorId;
                request.ReviewedAt = DateTime.UtcNow;
            }

            _context.Requests.Add(request);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547 && sqlEx.Message.Contains("FOREIGN KEY"))
                    return Conflict(new { error = "الطالب غير موجود", details = ex.Message });
                if (sqlEx.Number == 547 && sqlEx.Message.Contains("CHECK"))
                    return BadRequest(new { error = "بيانات الطلب غير صالحة", details = ex.Message });
                return StatusCode(500, new { error = "حدث خطأ في قاعدة البيانات", details = ex.Message });
            }

            if (actorId > 0)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    user_id = actorId,
                    action = "housing_approve_request",
                    target_table = "Requests",
                    target_id = request.Id,
                    action_at = DateTime.UtcNow,
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    user_agent = Request.Headers["User-Agent"].ToString()
                });
                if (isHousingCreator)
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        user_id = actorId,
                        action = "submit_cyber_review",
                        target_table = "Requests",
                        target_id = request.Id,
                        action_at = DateTime.UtcNow,
                        ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        user_agent = Request.Headers["User-Agent"].ToString()
                    });
                }
            }

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            var notifRoles = isHousingCreator ? new[] { "cyber" } : new[] { "admin", "supervisor", "cyber" };
            var notifMsg = isHousingCreator
                ? $"تم تقديم طلب جديد وإحالته للمراجعة الإلكترونية ({reqNum})"
                : $"تم تقديم طلب جديد ({reqNum})";
            foreach (var r in notifRoles)
            {
                _context.Notifications.Add(new Notification
                {
                    request_id = request.Id,
                    channel = "in_app",
                    recipient_role = r,
                    message = notifMsg,
                    status = "pending",
                    sent_at = DateTime.UtcNow
                });
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547)
                    return Conflict(new { error = "خطأ في الإشعارات", details = ex.Message });
                return StatusCode(500, new { error = "حدث خطأ في قاعدة البيانات", details = ex.Message });
            }

            return Ok(request);
        }

        [Authorize(Roles = "admin,supervisor,cyber")]
        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewRequest(int id, [FromBody] ReviewDto dto)
        {
            var req = await _context.Requests.FindAsync(id);
            if (req == null) return NotFound();

            var actorRole = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            var oldStatus = req.Status;

            var allowed = (actorRole, req.Status, dto.Status) switch
            {
                ("admin" or "supervisor", "submitted", "housing_approved" or "housing_rejected") => true,
                ("admin", "housing_approved", "cyber_review") => true,
                ("admin" or "cyber", "cyber_review", "cyber_approved" or "cyber_rejected") => true,
                ("admin" or "cyber", "cyber_approved", "ready_for_provisioning") => true,
                ("admin", "ready_for_provisioning", "completed") => true,
                _ => false
            };

            if (!allowed)
                return BadRequest(new { message = "Transition not allowed for this role" });

            req.Status = dto.Status;
            req.Notes = dto.Notes;

            if (dto.Status == "housing_approved" || dto.Status == "housing_rejected")
            {
                req.HousingReviewedBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.HousingReviewedAt = DateTime.UtcNow;
                req.HousingNotes = dto.Notes;
            }
            else if (dto.Status == "cyber_approved" || dto.Status == "cyber_rejected")
            {
                req.CyberReviewedBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.CyberReviewedAt = DateTime.UtcNow;
                req.CyberNotes = dto.Notes;
            }
            else if (dto.Status == "ready_for_provisioning")
            {
                req.ReadyForProvisioningBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.ReadyForProvisioningAt = DateTime.UtcNow;
            }
            else if (dto.Status == "completed")
            {
                var student = await _context.Students.FindAsync(req.StudentId);
                if (student != null)
                {
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var ua = Request.Headers["User-Agent"].ToString();
                    var provResult = await _adProvisioning.ProvisionAsync(student, actorId, ip, ua);
                    if (!provResult.Success)
                    {
                        _logger.LogError("AD provisioning FAILED for student {Id}: {Error} | StackTrace: {Stack}", student.student_id, provResult.Error, provResult.StackTrace);
                        return StatusCode(500, new { message = "AD account creation failed. Request not completed.", error = provResult.Error, stackTrace = provResult.StackTrace });
                    }

                    _logger.LogInformation("AD account created for student {Id}: {Sam}", student.student_id, provResult.SamAccountName);
                    var syncResult = await _adProvisioning.SyncExtensionAttributesAsync(student, actorId);
                    if (!syncResult.Success)
                        _logger.LogWarning("Extension attribute sync failed for student {Id}: {Error}", student.student_id, syncResult.Error);
                }

                req.CompletedBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.CompletedAt = DateTime.UtcNow;
                if (student != null && student.status != "left")
                {
                    student.status = "active";
                }
            }

            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedBy = actorId;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547)
                    return Conflict(new { error = "خطأ في تحديث الطلب", details = ex.Message });
                return StatusCode(500, new { error = "حدث خطأ في قاعدة البيانات", details = ex.Message });
            }

            var reviewAction = dto.Status switch
            {
                "housing_approved" => "housing_approve_request",
                "housing_rejected" => "housing_reject_request",
                "cyber_review" => "submit_cyber_review",
                "cyber_approved" => "cyber_approve_request",
                "cyber_rejected" => "cyber_reject_request",
                "ready_for_provisioning" => "ready_for_provisioning_request",
                "bulk_registration_completed" => "bulk_registration_completed",
                "completed" => "complete_request",
                _ => null
            };

            if (reviewAction != null)
            {
                var changes = new List<AuditChangeLog>();
                if (oldStatus != dto.Status)
                    changes.Add(new AuditChangeLog { FieldName = "Status", OldValue = oldStatus, NewValue = dto.Status });

                _context.AuditLogs.Add(new AuditLog
                {
                    user_id = actorId,
                    action = reviewAction,
                    target_table = "Requests",
                    target_id = req.Id,
                    action_at = DateTime.UtcNow,
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    AuditChangeLogs = changes.Count > 0 ? changes : null
                });

                var reqNum = req.RequestNumber ?? $"{DateTime.UtcNow.Year}-{req.Id:D6}";
                var notifRoles = dto.Status switch
                {
                    "housing_approved" => new[] { "admin", "cyber" },
                    "housing_rejected" => new[] { "admin", req.RequestedByRole ?? "supervisor" },
                    "cyber_review" => new[] { "cyber" },
                    "cyber_approved" => new[] { "admin", "supervisor", req.RequestedByRole ?? "admin" },
                    "cyber_rejected" => new[] { req.RequestedByRole ?? "admin" },
                    "ready_for_provisioning" => new[] { "admin", "supervisor" },
                    "completed" => new[] { "admin", "supervisor", req.RequestedByRole ?? "admin" },
                    _ => Array.Empty<string>()
                };
                var notifMsg = dto.Status switch
                {
                    "housing_approved" => $"تمت الموافقة على الطلب ({reqNum}) من قبل لجنة الإسكان",
                    "housing_rejected" => $"تم رفض الطلب ({reqNum}) من قبل لجنة الإسكان",
                    "cyber_review" => $"تم إحالة الطلب ({reqNum}) إلى المراجعة الإلكترونية",
                    "cyber_approved" => $"تمت الموافقة الإلكترونية على الطلب ({reqNum})",
                    "cyber_rejected" => $"تم الرفض الإلكتروني للطلب ({reqNum})",
                    "ready_for_provisioning" => $"الطلب ({reqNum}) جاهز لإنشاء حساب شبكة السكن",
                    "completed" => $"تم إكمال الطلب ({reqNum})",
                    _ => ""
                };
                foreach (var r in notifRoles)
                {
                    _context.Notifications.Add(new Notification
                    {
                        request_id = req.Id,
                        channel = "in_app",
                        recipient_role = r,
                        message = notifMsg,
                        status = "pending",
                        sent_at = DateTime.UtcNow
                    });
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
                {
                    if (sqlEx.Number == 547)
                        return Conflict(new { error = "خطأ في الإشعارات", details = ex.Message });
                    return StatusCode(500, new { error = "حدث خطأ في قاعدة البيانات", details = ex.Message });
                }
            }

            return Ok(req);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateRequest(int id, [FromBody] UpdateRequestDto dto)
        {
            var req = await _context.Requests.FindAsync(id);
            if (req == null) return NotFound();

            if (dto.BulkRequestId.HasValue)
            {
                req.BulkRequestId = dto.BulkRequestId.Value;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547)
                    return Conflict(new { error = "خطأ في تحديث الطلب", details = ex.Message });
                return StatusCode(500, new { error = "حدث خطأ في قاعدة البيانات", details = ex.Message });
            }

            return Ok(req);
        }
    }

    public class ReviewDto
    {
        public string? Status { get; set; }
        public int ReviewedBy { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateRequestDto
    {
        public int? BulkRequestId { get; set; }
    }
}
