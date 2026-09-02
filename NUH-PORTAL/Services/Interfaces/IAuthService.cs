namespace NUH_PORTAL.Services.Interfaces
{
    // مصادقة الموظف (AD أولًا ثم fallback محلي) + نشاط الجلسة + الخروج.
    // ⚠️ LoginAsync و SetPasswordAsync اتشالوا مع مسارَي api/Auth - شوف
    //    التعليق في AuthController.
    public interface IAuthService
    {
        // نفس فلو الدخول بالظبط (AD أولًا ثم fallback) بس بترجع المستخدم نفسه - بيستخدمها مسار الكوكي
        Task<NUH_PORTAL.Models.User> AuthenticateAsync(string username, string password);
        void RecordActivity();

        // ⚠️ عدّاد النشاط مفتاحه رقم المستخدم لا الجلسة، فهو بيعيش بعد الخروج.
        //    لازم يتمسح عند بداية أي جلسة جديدة وعند نهاية أي جلسة - وإلا الختم
        //    القديم بيقفل الجلسة الجديدة أول نداء API. شوف التعليق في
        //    AccountController.Login و Program.cs.
        void ResetActivity(int userId);

        Task LogoutAsync();
    }
}
