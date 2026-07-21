using AutoMapper;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Otp;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // تدفق الـ OTP — اتنقل من OtpController (التوكن بقى من ITokenService الموحّد)
    public class OtpFlowService : AppServiceBase, IOtpFlowService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<User> _users;
        private readonly OtpService _otpService;
        private readonly SmsService _smsService;
        private readonly IWorkflowService _workflow;   // بيسمح بتسجيل audit بدون مستخدم (otp_sent)
        private readonly ITokenService _tokens;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _http;

        public OtpFlowService(
            IRepository<Student> students,
            IRepository<User> users,
            OtpService otpService,
            SmsService smsService,
            IWorkflowService workflow,
            ITokenService tokens,
            IConfiguration config,
            IHttpContextAccessor http,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _users = users;
            _otpService = otpService;
            _smsService = smsService;
            _workflow = workflow;
            _tokens = tokens;
            _config = config;
            _http = http;
        }

        private (string? ip, string ua) ClientInfo()
        {
            var ctx = _http.HttpContext;
            return (ctx?.Connection.RemoteIpAddress?.ToString(), ctx?.Request.Headers.UserAgent.ToString() ?? "");
        }

        public async Task<SendOtpResultDto> SendAsync(SendOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Mobile))
                throw new UserFriendlyException("رقم الجوال مطلوب", 400);

            var (ip, ua) = ClientInfo();
            var otpMode = _config["Otp:Mode"] ?? "Sms";
            string code;
            DateTime expiresAt;

            try
            {
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
            }
            catch (InvalidOperationException ex)
            {
                throw new UserFriendlyException(ex.Message, 400);
            }

            await _workflow.LogAuditAsync(null, "otp_sent", "OTPVerifications", 0, ip, ua);

            return new SendOtpResultDto
            {
                Message = "تم إرسال رمز التحقق",
                ExpiresAt = expiresAt,
                Mode = otpMode.ToLowerInvariant()
            };
        }

        public async Task<VerifyOtpResultDto> VerifyAsync(VerifyOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Mobile) || string.IsNullOrWhiteSpace(request.Code))
                throw new UserFriendlyException("رقم الجوال ورمز التحقق مطلوبان", 400);

            var valid = await _otpService.VerifyOtpAsync(request.Mobile, request.Code);
            if (!valid)
                throw new UserFriendlyException("رمز التحقق غير صحيح أو منتهي الصلاحية", 400);

            var student = await _students.FindAsync(s => s.phone == request.Mobile);
            var user = await _users.FindAsync(u => u.mobile == request.Mobile);

            if (user == null)
            {
                user = new User
                {
                    username = "student_" + (student?.student_id ?? request.Mobile.Replace("+", "").Replace(" ", "")),
                    full_name = student?.full_name ?? "طالب",
                    email = request.Mobile + "@student.nu.edu.sa",
                    role = "user",
                    mobile = request.Mobile,
                    is_active = true,
                    created_at = DateTime.UtcNow
                };
                await _users.AddAsync(user);
                await UnitOfWork.SaveAsync();
            }
            else if (user.role != "user")
            {
                user.role = "user";
                await UnitOfWork.SaveAsync();
            }

            var token = _tokens.GenerateToken(user);

            var (ip, ua) = ClientInfo();
            await _workflow.LogAuditAsync(user.Id, "otp_verified", "OTPVerifications", 0, ip, ua);

            return new VerifyOtpResultDto
            {
                Message = "تم التحقق بنجاح",
                Token = token,
                User = new OtpUserDto
                {
                    id = user.Id,
                    username = user.username,
                    full_name = user.full_name,
                    role = "user",
                    student_id = student?.student_id,
                    student_name = student?.full_name
                }
            };
        }
    }
}
