using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NUH_PORTAL.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class OtpController : ControllerBase
    {
        private readonly OtpService _otpService;
        private readonly SmsService _smsService;
        private readonly AppDbContext _context;
        private readonly ILogger<OtpController> _logger;
        private readonly IConfiguration _config;
        private readonly WorkflowService _workflowService;

        public OtpController(OtpService otpService, SmsService smsService, AppDbContext context,
            ILogger<OtpController> logger, WorkflowService workflowService, IConfiguration config)
        {
            _otpService = otpService;
            _smsService = smsService;
            _context = context;
            _logger = logger;
            _workflowService = workflowService;
            _config = config;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Mobile))
                return BadRequest(new { message = "رقم الجوال مطلوب" });

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var otpMode = _config["Otp:Mode"] ?? "Sms";
                string code;
                DateTime expiresAt;

                if (otpMode == "Static")
                {
                    var staticCode = _config["Otp:StaticCode"] ?? "123456";
                    (code, expiresAt) = await _otpService.GenerateOtpAsync(request.Mobile, ip, staticCode);
                }
                else
                {
                    (code, expiresAt) = await _otpService.GenerateOtpAsync(request.Mobile, ip);
                    await _smsService.SendOtpAsync(request.Mobile, code);
                }

                var ua = HttpContext.Request.Headers.UserAgent.ToString();
                await _workflowService.LogAuditAsync(null, "otp_sent", "OTPVerifications", 0, ip, ua);

                return Ok(new
                {
                    message = "تم إرسال رمز التحقق",
                    expiresAt,
                    mode = otpMode.ToLowerInvariant()
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Mobile) || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { message = "رقم الجوال ورمز التحقق مطلوبان" });

            var valid = await _otpService.VerifyOtpAsync(request.Mobile, request.Code);

            if (!valid)
                return BadRequest(new { message = "رمز التحقق غير صحيح أو منتهي الصلاحية" });

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.phone == request.Mobile);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.mobile == request.Mobile);

            if (user == null)
            {
                user = new User
                {
                    username = "student_" + (student?.student_id ?? request.Mobile.Replace("+","").Replace(" ","")),
                    full_name = student?.full_name ?? "طالب",
                    email = request.Mobile + "@student.nu.edu.sa",
                    role = "user",
                    mobile = request.Mobile,
                    is_active = true,
                    created_at = DateTime.UtcNow
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else if (user.role != "user")
            {
                user.role = "user";
                await _context.SaveChangesAsync();
            }

            var token = GenerateJwtToken(user);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(user.Id, "otp_verified", "OTPVerifications", 0, ip, ua);

            return Ok(new
            {
                message = "تم التحقق بنجاح",
                token,
                user = new
                {
                    id = user.Id,
                    username = user.username,
                    full_name = user.full_name,
                    role = "user",
                    student_id = student?.student_id,
                    student_name = student?.full_name
                }
            });
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.username!),
                new Claim(ClaimTypes.Role, (user.role ?? "User").ToLowerInvariant())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class SendOtpRequest
    {
        public string Mobile { get; set; } = string.Empty;
    }

    public class VerifyOtpRequest
    {
        public string Mobile { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
