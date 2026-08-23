using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        // ⚠️ اتشال من هنا: POST api/Auth/Login و POST api/Auth/SetPassword.
        //
        //    Login: باب دخول تاني بيصرف توكن، ومفيش أي واجهة بتستخدمه —
        //    دخول الموظفين الفعلي على POST /Account/Login (مسار الكوكي).
        //    بابان للدخول معناهما تشديد على واحد ونسيان التاني، وده اللي حصل
        //    فعلًا: حدّ المحاولات كان متحطّط على المسار ده وحده والباب الحقيقي
        //    من غير حدّ (اتصلّح وقتها، وهنا بنقفل الباب اللي محدش بيدخل منه).
        //
        //    SetPassword: كان بيضبط كلمة مرور *محلية* لأي حساب بصلاحية
        //    users.manage. والدخول بيقبل الكلمة المحلية كـ fallback لو الأكتف
        //    دايركتوري مش متاح — يعني اللي بيدير المستخدمين كان يقدر يضبط
        //    كلمة سر لحساب الأدمن ويدخل بيها. ترقية صلاحيات كاملة من خانة
        //    «إدارة المستخدمين»، من مسار مفيش شاشة بتناديه أصلًا.
        //    تدوير كلمة الـ fallback (لو احتاجت) من DbSeeder لا من API مفتوح.

        // POST api/Auth/Ping
        [Authorize]
        [HttpPost("Ping")]
        public IActionResult Ping()
        {
            _service.RecordActivity();
            return Ok();
        }

    }
}
