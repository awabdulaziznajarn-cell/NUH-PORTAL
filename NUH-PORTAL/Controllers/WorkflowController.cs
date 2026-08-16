using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Workflow;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق الطوابير والاعتماد في IWorkflowActionService
    // الدخول للـ endpoints بصلاحية requests.view، أما مين يقدر يعتمد/يرفض فبيتحدّد
    // من *مرحلة* الطلب: WorkflowActionService بيطلب requests.reviewHousing أو
    // requests.reviewCyber أو requests.complete حسب الحالة الحالية.
    [Authorize(Policy = "requests.view")]
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

        // GET api/Workflow/queue/my-count — عدد الطلبات المستنية إجراء من المستخدم الحالي
        [HttpGet("queue/my-count")]
        public async Task<IActionResult> GetMyQueueCount()
            => Ok(new { count = await _service.GetMyQueueCountAsync() });

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
            await _service.RequestMoreInfoAsync(requestId, request.Notes, request.Fields);
            return Ok(new { message = "تم طلب معلومات إضافية" });
        }

        // GET api/Workflow/editable-fields
        // ⚠️ القائمة بتتقرا من RegistrationDataMapper.EditableFields — نفس المصدر
        //    اللي بيحسب فرق التعديلات ويطبّقها على سجل الطالب. لو الواجهة كتبت
        //    قائمة خاصة بيها، أول ما خانة تتضاف أو تتشال هيبقى عندنا قائمتين
        //    مختلفتين والمراجع يعلّم على خانة مش موجودة في الفورم.
        [HttpGet("editable-fields")]
        public IActionResult GetEditableFields()
            => Ok(NUH_PORTAL.Services.RegistrationDataMapper.EditableFields
                    .Select(f => new { key = f.Key, label = f.Label }));

        // GET api/Workflow/{requestId}/history
        [HttpGet("{requestId}/history")]
        public async Task<IActionResult> GetHistory(int requestId)
            => Ok(await _service.GetHistoryAsync(requestId));
    }
}
