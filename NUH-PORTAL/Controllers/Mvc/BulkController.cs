using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة التسجيل الجماعي Excel (MVC) — ويزارد 5 خطوات على نفس APIs الـ BulkRegistration
    // (دمج BulkRequestStudents في RegistrationData خطوة داتابيز منفصلة بعد اعتماد الصفحات)
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "admin,supervisor")]
    [Route("Bulk")]
    public class BulkController : Controller
    {
        // GET /Bulk
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
