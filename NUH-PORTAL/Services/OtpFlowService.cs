using MapsterMapper;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly IPermissionService _permissions;

        public OtpFlowService(
            IRepository<Student> students,
            IRepository<User> users,
            OtpService otpService,
            SmsService smsService,
            IWorkflowService workflow,
            ITokenService tokens,
            IConfiguration config,
            IHttpContextAccessor http,
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            IPermissionService permissions,
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
            _userManager = userManager;
            _roleManager = roleManager;
            _permissions = permissions;
        }

        // ضمان وجود دور "user" وإسناده للمستخدم
        private async Task EnsureUserRoleAsync(User user)
        {
            if (!await _roleManager.RoleExistsAsync("user"))
                await _roleManager.CreateAsync(new Role("user"));
            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("user"))
                await _userManager.AddToRoleAsync(user, "user");
        }

        // Students.phone متخزّن 9665XXXXXXXX، لكن الطالب بيكتب 05XXXXXXXX في شاشة
        // التحقق. من غير تطبيع، البحث عن الطالب برقمه بيفشل دايمًا، فبيتعمل حساب
        // اسمه "طالب" ومربوط بصيغة رقم مختلفة — وده كان بيظهر في مسار الطلب
        // وبيكسر أي مقارنة لاحقة بين الحسابين.
        // نفس منطق NormalizeMobile في RegistrationFlowService و normalizeSaudiMobile في الواجهة.
        private static string? NormalizeMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return null;
            var d = new string(mobile.Where(char.IsDigit).ToArray());
            if (d.StartsWith("00966")) d = d[2..];
            if (d.StartsWith("966")) d = d[3..];
            if (d.StartsWith("0")) d = d[1..];
            return d.Length == 9 && d[0] == '5' ? "966" + d : null;
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

            // الرمز بيتخزّن ويتحقق منه على الصيغة الموحّدة، عشان لو الطالب كتب
            // 05... وقت الإرسال و966... وقت التحقق (أو العكس) يفضل نفس السجل.
            var mobile = NormalizeMobile(request.Mobile) ?? request.Mobile.Trim();

            var (ip, ua) = ClientInfo();
            var otpMode = _config["Otp:Mode"] ?? "Sms";
            string code;
            DateTime expiresAt;

            try
            {
                if (otpMode == "Static")
                {
                    var staticCode = _config["Otp:StaticCode"] ?? "123456";
                    (code, expiresAt) = await _otpService.GenerateOtpAsync(mobile, ip, staticCode);
                }
                else
                {
                    (code, expiresAt) = await _otpService.GenerateOtpAsync(mobile, ip);
                    await _smsService.SendOtpAsync(mobile, code);
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

            var raw = request.Mobile.Trim();
            var mobile = NormalizeMobile(raw) ?? raw;

            // بنجرّب الصيغة الموحّدة الأول وبعدين اللي اتكتب — عشان أي رمز اتبعت
            // قبل التعديل ده (متخزّن بالصيغة الخام) يفضل يتحقق عادي.
            var valid = await _otpService.VerifyOtpAsync(mobile, request.Code)
                     || (mobile != raw && await _otpService.VerifyOtpAsync(raw, request.Code));
            if (!valid)
                throw new UserFriendlyException("رمز التحقق غير صحيح أو منتهي الصلاحية", 400);

            // ⚠️ البحث بالصيغتين. قبل كده كان بالصيغة اللي اتكتبت بس، فالطالب
            //    المسجّل بـ 9665... مكانش بيتلاقى لما يكتب 05...، وكان بيتعمل
            //    حساب جديد اسمه "طالب" بدل ما يترتبط ببياناته.
            var student = await _students.FindAsync(s => s.phone == mobile || s.phone == raw);
            var user = await _users.FindAsync(u => u.mobile == mobile || u.mobile == raw);

            if (user == null)
            {
                user = new User
                {
                    UserName = "student_" + (student?.student_id ?? mobile),
                    full_name = student?.full_name ?? "طالب",
                    Email = mobile + "@student.nu.edu.sa",
                    mobile = mobile,
                    is_active = true,
                    created_at = DateTime.UtcNow
                };
                await _userManager.CreateAsync(user);
                await EnsureUserRoleAsync(user);
            }
            else
            {
                // إصلاح ذاتي للحسابات القديمة: توحيد صيغة الرقم، وتعويض الاسم
                // اللي اتخزّن "طالب" لما البحث كان بيفشل. بيتصلح من أول دخول.
                var needsUpdate = false;
                if (user.mobile != mobile) { user.mobile = mobile; needsUpdate = true; }
                if (student?.full_name != null &&
                    (string.IsNullOrWhiteSpace(user.full_name) || user.full_name == "طالب"))
                {
                    user.full_name = student.full_name;
                    needsUpdate = true;
                }
                if (needsUpdate) await _userManager.UpdateAsync(user);

                await EnsureUserRoleAsync(user);
            }

            var perms = await _permissions.GetPermissionsForRolesAsync(new[] { "user" });
            var token = _tokens.GenerateToken(user, new[] { "user" }, perms);

            var (ip, ua) = ClientInfo();
            await _workflow.LogAuditAsync(user.Id, "otp_verified", "OTPVerifications", 0, ip, ua);

            return new VerifyOtpResultDto
            {
                Message = "تم التحقق بنجاح",
                Token = token,
                User = new OtpUserDto
                {
                    id = user.Id,
                    username = user.UserName,
                    full_name = user.full_name,
                    role = "user",
                    student_id = student?.student_id,
                    student_name = student?.full_name
                }
            };
        }
    }
}
