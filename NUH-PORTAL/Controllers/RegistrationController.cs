using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Registration;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — تدفق التسجيل الذاتي في IRegistrationFlowService
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class RegistrationController : ControllerBase
    {
        private readonly IRegistrationFlowService _service;

        public RegistrationController(IRegistrationFlowService service) => _service = service;

        // POST api/Registration/start
        [HttpPost("start")]
        public async Task<IActionResult> StartRegistration([FromBody] StartRegistrationRequest request)
        {
            var result = await _service.StartAsync(request);
            return Ok(new { message = result.Message, requestId = result.RequestId, requestNumber = result.RequestNumber });
        }

        // ====================================================================
        //  GET api/Registration/pledge — بنود التعهّد وبصمتها.
        //
        //  ⚠️ [AllowAnonymous] زي /api/lookups/terms بالظبط: الرد بنود عامة
        //     بيقراها أي طالب قبل ما يبدأ، مفيش فيه بيانات حد.
        //
        //  ⚠️ وموجودة مع إن /api/lookups/terms موجودة: دي بترجّع اللغتين مع
        //     النصّ المجمَّد وبصمته، وتلك بترجّع لغة واحدة بلا بصمة. لو الصفحة
        //     حسبت البصمة من اللي عرضته، الطالب اللي فاتح بالإنجليزية كان
        //     هيطلعله بصمة تانية لنفس البنود.
        // ====================================================================
        [AllowAnonymous]
        [HttpGet("pledge")]
        public async Task<IActionResult> Pledge()
            => Ok(await _service.GetPledgeDocumentAsync());

        // POST api/Registration/{requestId}/declarations
        [HttpPost("{requestId}/declarations")]
        public async Task<IActionResult> AcceptDeclarations(int requestId, [FromBody] AcceptDeclarationsRequest request)
        {
            await _service.AcceptDeclarationsAsync(requestId, request);
            return Ok(new { message = "تم قبول الإقرار" });
        }

        // GET api/Registration/open-statuses
        // ⚠️ موجود عشان شاشة الجوال (register-phone.html) كانت شايلة نسخة
        //    تالتة من قائمة الحالات المفتوحة مكتوبة بالإيد. صفحة ثابتة في
        //    wwwroot فما ينفعش نحقن لها الجدول زي شاشات الموظفين، فبتقراها
        //    من هنا — والمصدر واحد: Core/RequestWorkflow.OpenStatuses.
        //    [AllowAnonymous] لأن الرد أسماء مراحل لا بيانات أي طالب، والفحص
        //    ده تسهيل للطالب أصلًا: الحارس الحقيقي عند الإرسال على السيرفر.
        [AllowAnonymous]
        [HttpGet("open-statuses")]
        public IActionResult GetOpenStatuses()
            => Ok(NUH_PORTAL.Core.RequestWorkflow.OpenStatuses);

        // GET api/Registration/my-requests?mobile=
        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests([FromQuery] string? mobile = null)
            => Ok(await _service.GetMyRequestsAsync(mobile));

        // GET api/Registration/my-requests/{requestId}
        [HttpGet("my-requests/{requestId}")]
        public async Task<IActionResult> GetMyRequestDetail(int requestId)
            => Ok(await _service.GetMyRequestDetailAsync(requestId));

        // POST api/Registration/check-duplicate
        // فحص مبكر للتكرار أثناء تعبئة النموذج (الرقم الجامعي + رقم الهوية معًا)
        [HttpPost("check-duplicate")]
        public async Task<IActionResult> CheckDuplicate([FromBody] DuplicateCheckRequest request)
            => Ok(await _service.CheckDuplicateAsync(request));

        // POST api/Registration/{requestId}/resubmit
        [HttpPost("{requestId}/resubmit")]
        public async Task<IActionResult> ResubmitRequest(int requestId, [FromBody] ResubmitRequest request)
        {
            await _service.ResubmitAsync(requestId, request);
            return Ok(new { message = "تم إعادة تقديم الطلب" });
        }
    }
}
