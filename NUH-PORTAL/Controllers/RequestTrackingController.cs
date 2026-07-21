using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // تتبع عام للطلبات (بدون تسجيل دخول) — كنترولر رفيع، المنطق في IRequestTrackingService
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

        // GET api/RequestTracking/{requestNumber}
        [HttpGet("{requestNumber}")]
        public async Task<IActionResult> TrackByNumber(string requestNumber)
            => Ok(await _service.TrackByNumberAsync(requestNumber));
    }
}
