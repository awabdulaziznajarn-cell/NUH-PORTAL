using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Services;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    [Authorize(Roles = "admin,supervisor,cyber")]
    [Route("api/[controller]")]
    [ApiController]
    public class WorkflowController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RegistrationService _registrationService;
        private readonly WorkflowService _workflowService;
        private readonly ILogger<WorkflowController> _logger;

        public WorkflowController(AppDbContext context, RegistrationService registrationService,
            WorkflowService workflowService, ILogger<WorkflowController> logger)
        {
            _context = context;
            _registrationService = registrationService;
            _workflowService = workflowService;
            _logger = logger;
        }

        [HttpGet("queue")]
        public async Task<IActionResult> GetQueue([FromQuery] string? stage)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

            string? targetStage = stage;
            if (string.IsNullOrEmpty(targetStage))
            {
                targetStage = userRole switch
                {
                    "supervisor" => "pending_supervisor",
                    "cyber" => "pending_cyber",
                    "admin" => "ready_for_provisioning",
                    _ => null
                };
            }

            if (string.IsNullOrEmpty(targetStage))
                return BadRequest(new { message = "لا يمكن تحديد المرحلة" });

            var requests = await _context.Requests
                .Include(r => r.Student)
                .Where(r => r.RequestType == "self_registration" && r.Status == targetStage)
                .OrderBy(r => r.SubmittedAt)
                .Select(r => new
                {
                    r.Id,
                    r.RequestNumber,
                    r.Status,
                    r.SubmittedAt,
                    StudentName = r.Student!.full_name,
                    StudentId = r.Student.student_id,
                    r.Notes
                })
                .ToListAsync();

            return Ok(requests);
        }

        [HttpGet("queue/counts")]
        public async Task<IActionResult> GetQueueCounts()
        {
            var counts = await _context.Requests
                .Where(r => r.RequestType == "self_registration")
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(counts);
        }

        [HttpPost("{requestId}/approve")]
        public async Task<IActionResult> Approve(int requestId, [FromBody] WorkflowActionRequest request)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userRole))
                return Unauthorized();

            bool result = userRole switch
            {
                "supervisor" => await _registrationService.ApproveAsSupervisorAsync(requestId, actorId, request.Notes),
                "cyber" => await _registrationService.ApproveAsCyberAsync(requestId, actorId, request.Notes),
                "admin" => await _registrationService.ApproveAsAdminAsync(requestId, actorId, request.Notes),
                _ => false
            };

            if (!result)
                return BadRequest(new { message = "لا يمكن اعتماد الطلب في المرحلة الحالية" });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(actorId, "workflow_approved", "Requests", requestId, ip, ua);

            return Ok(new { message = "تم اعتماد الطلب بنجاح" });
        }

        [HttpPost("{requestId}/reject")]
        public async Task<IActionResult> Reject(int requestId, [FromBody] WorkflowActionRequest request)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userRole))
                return Unauthorized();

            bool result = userRole switch
            {
                "supervisor" => await _registrationService.RejectAsSupervisorAsync(requestId, actorId, request.Notes),
                "cyber" => await _registrationService.RejectAsCyberAsync(requestId, actorId, request.Notes),
                "admin" => await _registrationService.RejectAsAdminAsync(requestId, actorId, request.Notes),
                _ => false
            };

            if (!result)
                return BadRequest(new { message = "لا يمكن رفض الطلب في المرحلة الحالية" });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(actorId, "workflow_rejected", "Requests", requestId, ip, ua);

            return Ok(new { message = "تم رفض الطلب" });
        }

        [HttpPost("{requestId}/request-info")]
        public async Task<IActionResult> RequestMoreInfo(int requestId, [FromBody] WorkflowActionRequest request)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Notes))
                return BadRequest(new { message = "الملاحظات مطلوبة لطلب معلومات إضافية" });

            var result = await _registrationService.RequestMoreInfoAsync(requestId, actorId, request.Notes);

            if (!result)
                return BadRequest(new { message = "لا يمكن طلب معلومات إضافية لهذا الطلب" });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(actorId, "info_requested", "Requests", requestId, ip, ua);

            return Ok(new { message = "تم طلب معلومات إضافية" });
        }

        [HttpGet("{requestId}/history")]
        public async Task<IActionResult> GetHistory(int requestId)
        {
            var request = await _context.Requests.Include(r => r.Student).FirstOrDefaultAsync(r => r.Id == requestId);
            var studentName = request?.Student?.full_name;

            var history = await _workflowService.GetHistoryAsync(requestId);
            return Ok(history.Select(h => new
            {
                h.FromStage,
                h.ToStage,
                h.ActionDate,
                h.Notes,
                ActorName = h.Actor != null
                    ? (h.Actor.role == "user" && studentName != null ? studentName : h.Actor.full_name ?? h.Actor.username)
                    : null
            }));
        }
    }

    public class WorkflowActionRequest
    {
        public string? Notes { get; set; }
    }
}
