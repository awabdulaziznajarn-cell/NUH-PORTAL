using NUH_PORTAL.DTOs.Auth;

namespace NUH_PORTAL.Services.Interfaces
{
    // تسجيل الدخول (AD أولًا ثم fallback محلي) + إدارة كلمة المرور + الخروج
    public interface IAuthService
    {
        Task<LoginResultDto> LoginAsync(LoginRequest request);
        Task SetPasswordAsync(SetPasswordRequest request);
        void RecordActivity();
        Task LogoutAsync();
    }
}
