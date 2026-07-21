using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Auth;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق المصادقة في IAuthService
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;

        public AuthController(IAuthService service) => _service = service;

        // POST api/Auth/Login
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _service.LoginAsync(request);
            return Ok(new { message = result.Message, token = result.Token, user = result.User });
        }

        // POST api/Auth/SetPassword
        [Authorize(Roles = "admin")]
        [HttpPost("SetPassword")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            await _service.SetPasswordAsync(request);
            return Ok(new { message = "تم ضبط كلمة المرور بنجاح" });
        }

        // POST api/Auth/Ping
        [Authorize]
        [HttpPost("Ping")]
        public IActionResult Ping()
        {
            _service.RecordActivity();
            return Ok();
        }

        // POST api/Auth/Logout
        [Authorize]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            await _service.LogoutAsync();
            return Ok();
        }
    }
}
