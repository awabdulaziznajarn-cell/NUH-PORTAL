using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Workflow;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق الطوابير والاعتماد في IWorkflowActionService
    [Authorize(Roles = "admin,supervisor,cyber")]
    [Route("api/[controller]")]
    [ApiController]
    public class WorkflowController : ControllerBase
    {
        private readonly IWorkflowActionService _service;

        public WorkflowController(IWorkflowActionService service) => _service = service;

        // GET api/Workflow/queue?stage=
        [HttpGet("queue")]
        public async Task<IActionResult> GetQueue([FromQuery] string? stage)
            => Ok(await _service.GetQueueAsync(stage));

        // GET api/Workflow/queue/counts
        [HttpGet("queue/counts")]
        public async Task<IActionResult> GetQueueCounts()
            => Ok(await _service.GetQueueCountsAsync());

        // POST api/Workflow/{requestId}/approve
        [HttpPost("{requestId}/approve")]
        public async Task<IActionResult> Approve(int requestId, [FromBody] WorkflowActionRequest request)
        {
            await _service.ApproveAsync(requestId, request.Notes);
            return Ok(new { message = "تم اعتماد الطلب بنجاح" });
        }

        // POST api/Workflow/{requestId}/reject
        [HttpPost("{requestId}/reject")]
        public async Task<IActionResult> Reject(int requestId, [FromBody] WorkflowActionRequest request)
        {
            await _service.RejectAsync(requestId, request.Notes);
            return Ok(new { message = "تم رفض الطلب" });
        }

        // POST api/Workflow/{requestId}/request-info
        [HttpPost("{requestId}/request-info")]
        public async Task<IActionResult> RequestMoreInfo(int requestId, [FromBody] WorkflowActionRequest request)
        {
            await _service.RequestMoreInfoAsync(requestId, request.Notes);
            return Ok(new { message = "تم طلب معلومات إضافية" });
        }

        // GET api/Workflow/{requestId}/history
        [HttpGet("{requestId}/history")]
        public async Task<IActionResult> GetHistory(int requestId)
            => Ok(await _service.GetHistoryAsync(requestId));
    }
}
