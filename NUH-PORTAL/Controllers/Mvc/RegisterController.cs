using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة تسجيل طالب جديد (MVC) — فورم ثابت، والإرسال بيتم بالـ JS على نفس الـ APIs القديمة
    // (POST /api/students ثم POST /api/requests) — نفس سلوك register_student.html بالحرف
    // الوصول للصفحة بالصلاحية مش بالدور — عشان أي دور جديد ياخد الصلاحية ويشتغل
    // من غير ما نعدّل الكود. الشاشة نفسها بتختفي من القائمة الجانبية كمان.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "students.create")]
    [Route("Register")]
    public class RegisterController : Controller
    {
        // GET /Register
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
