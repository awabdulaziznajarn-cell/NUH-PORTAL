using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // مركز التقارير (MVC) — بيعيد استخدام منطق صفحة التقارير القديمة كما هو (reports-page.js)
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [Route("Reports")]
    public class ReportsController : Controller
    {
        // GET /Reports
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
