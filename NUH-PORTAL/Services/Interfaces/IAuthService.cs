namespace NUH_PORTAL.Services.Interfaces
{
    // مصادقة الموظف (AD أولًا ثم fallback محلي) + نشاط الجلسة + الخروج.
    // ⚠️ LoginAsync و SetPasswordAsync اتشالوا مع مسارَي api/Auth — شوف
    //    التعليق في AuthController.
    public interface IAuthService
    {
        // نفس فلو الدخول بالظبط (AD أولًا ثم fallback) بس بترجع المستخدم نفسه — بيستخدمها مسار الكوكي
        Task<NUH_PORTAL.Models.User> AuthenticateAsync(string username, string password);
        void RecordActivity();
        Task LogoutAsync();
    }
}
