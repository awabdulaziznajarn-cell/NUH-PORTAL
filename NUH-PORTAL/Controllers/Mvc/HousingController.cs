using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة إدارة حسابات السكن (MVC) — admin فقط (نفس صلاحية API الـ HousingAccountManagement)
    // بتعيد استخدام housing-management.js القديم زي ما هو — نفس السلوك بالظبط بشكل موحّد
    // الوصول للصفحة بالصلاحية مش بالدور — عشان أي دور جديد ياخد الصلاحية ويشتغل
    // من غير ما نعدّل الكود. الشاشة نفسها بتختفي من القائمة الجانبية كمان.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "housing.view")]
    [Route("Housing")]
    public class HousingController : Controller
    {
        // GET /Housing
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
