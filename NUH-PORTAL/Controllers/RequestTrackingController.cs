using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // ========================================================================
    //  ⚠️ الكنترولر ده كان [AllowAnonymous] — أي حد من غير تسجيل دخول كان يقدر
    //     ينادي /api/RequestTracking/{رقم الطلب}. وأرقام الطلبات متسلسلة
    //     (2026-000001, 000002, ...) فكان ممكن حد يعدّي عليها بالترتيب ويسحب
    //     أسماء الطلاب وحالات طلباتهم — وبعد إضافة سبب الرفض، الملاحظات كمان.
    //
    //     الطالب بقى بيتابع طلبه من صفحة register/track-request.html عن طريق
    //     تحقق OTP برقم جواله، وبتنادي /api/Registration/my-requests (مصادَق
    //     عليه وبيرجّع طلبات صاحب الرقم بس).
    //
    //     ⚠️ قرار مؤقت (إداري، مش تقني): اتقرر نسيبها مفتوحة لحد ما الإدارة
    //     تحدد، لأن ربطها بـ OTP بيستهلك رصيد الرسائل المخصص لتسجيل الطلبات.
    //
    //     لو اتقرر التشديد لاحقًا، فيه حلّين بدون أي تكلفة رسائل:
    //       (١) طلب رقم الطلب + آخر ٤ أرقام من الجوال مع بعض — التخمين بيبقى
    //           غير عملي لأن المهاجم محتاج المعلومتين مع بعض.
    //       (٢) [Authorize(Policy = "requests.view")] وتحويل الطالب على
    //           /api/Registration/my-requests بعد تحقق OTP.
    // ========================================================================
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class RequestTrackingController : ControllerBase
    {
        private readonly IRequestTrackingService _service;

        public RequestTrackingController(IRequestTrackingService service) => _service = service;

        // GET api/RequestTracking/by-mobile/{mobile}
        [HttpGet("by-mobile/{mobile}")]
        public async Task<IActionResult> TrackByMobile(string mobile)
            => Ok(await _service.TrackByMobileAsync(mobile));

        // GET api/RequestTracking/{requestNumber}?last4=1234
        // last4 = آخر ٤ أرقام من جوال الطالب — التحقق منها في الخدمة (على السيرفر).
        [HttpGet("{requestNumber}")]
        public async Task<IActionResult> TrackByNumber(string requestNumber, [FromQuery] string? last4 = null)
            => Ok(await _service.TrackByNumberAsync(requestNumber, last4));
    }
}
