using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // مركز التقارير (MVC) — بيعيد استخدام منطق صفحة التقارير القديمة كما هو (reports-page.js)
    // الوصول للصفحة بالصلاحية مش بالدور — عشان أي دور جديد ياخد الصلاحية ويشتغل
    // من غير ما نعدّل الكود. الشاشة نفسها بتختفي من القائمة الجانبية كمان.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "reports.view")]
    [Route("Reports")]
    public class ReportsController : Controller
    {
        // GET /Reports
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
