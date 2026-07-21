using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Otp;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — تدفق الـ OTP في IOtpFlowService
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class OtpController : ControllerBase
    {
        private readonly IOtpFlowService _service;

        public OtpController(IOtpFlowService service) => _service = service;

        // POST api/Otp/send
        [HttpPost("send")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            var result = await _service.SendAsync(request);
            return Ok(new { message = result.Message, expiresAt = result.ExpiresAt, mode = result.Mode });
        }

        // POST api/Otp/verify
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            var result = await _service.VerifyAsync(request);
            return Ok(new { message = result.Message, token = result.Token, user = result.User });
        }
    }
}
