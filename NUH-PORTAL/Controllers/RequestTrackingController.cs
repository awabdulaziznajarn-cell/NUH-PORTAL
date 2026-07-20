using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Services;

namespace NUH_PORTAL.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class RequestTrackingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly WorkflowService _workflowService;
        private readonly ILogger<RequestTrackingController> _logger;

        public RequestTrackingController(AppDbContext context, WorkflowService workflowService, ILogger<RequestTrackingController> logger)
        {
            _context = context;
            _workflowService = workflowService;
            _logger = logger;
        }

        private static string NormalizePhone(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return mobile;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 10 && digits.StartsWith("05"))
                return "9665" + digits[2..];
            if (digits.Length == 9 && digits.StartsWith("5"))
                return "966" + digits;
            return digits;
        }

        [HttpGet("by-mobile/{mobile}")]
        public async Task<IActionResult> TrackByMobile(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return BadRequest(new { message = "رقم الجوال مطلوب" });

            var normalized = NormalizePhone(mobile);

            var studentIds = await _context.Students
                .Where(s => s.phone == mobile || s.phone == normalized)
                .Select(s => s.Id)
                .ToListAsync();

            if (studentIds.Count == 0)
                return NotFound(new { message = "لا توجد طلبات مرتبطة بهذا الرقم" });

            var requests = await _context.Requests
                .Include(r => r.Student)
                .Where(r => r.RequestType == "self_registration" && studentIds.Contains(r.StudentId))
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new
                {
                    r.RequestNumber,
                    r.Status,
                    r.SubmittedAt,
                    StudentName = r.Student!.full_name
                })
                .ToListAsync();

            return Ok(requests);
        }

        [HttpGet("{requestNumber}")]
        public async Task<IActionResult> TrackByNumber(string requestNumber)
        {
            if (string.IsNullOrWhiteSpace(requestNumber))
                return BadRequest(new { message = "رقم الطلب مطلوب" });

            var request = await _context.Requests
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber && r.RequestType == "self_registration");

            if (request == null)
                return NotFound(new { message = "الطلب غير موجود" });

            var history = await _workflowService.GetHistoryAsync(request.Id);

            return Ok(new
            {
                Id = request.Id,
                request.RequestNumber,
                request.Status,
                request.SubmittedAt,
                StudentName = request.Student?.full_name,
                AdUsername = request.Student?.ad_username,
                History = history.Select(h => new
                {
                    h.ToStage,
                    h.ActionDate,
                    h.Notes,
                    ActorName = h.Actor?.full_name ?? h.Actor?.username
                })
            });
        }
    }
}
