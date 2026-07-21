using NUH_PORTAL.DTOs.Auth;

namespace NUH_PORTAL.Services.Interfaces
{
    // تسجيل الدخول (AD أولًا ثم fallback محلي) + إدارة كلمة المرور + الخروج
    public interface IAuthService
    {
        Task<LoginResultDto> LoginAsync(LoginRequest request);
        // نفس فلو الدخول بالظبط (AD أولًا ثم fallback) بس بترجع المستخدم نفسه — بيستخدمها مسار الكوكي
        Task<NUH_PORTAL.Models.User> AuthenticateAsync(string username, string password);
        Task SetPasswordAsync(SetPasswordRequest request);
        void RecordActivity();
        Task LogoutAsync();
    }
}
