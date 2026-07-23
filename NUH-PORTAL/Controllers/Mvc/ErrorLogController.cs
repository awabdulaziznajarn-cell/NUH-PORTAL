using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة سجل الأخطاء (MVC) — محميّة بصلاحية errorLogs.view. البيانات من /api/logs/errors.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "errorLogs.view")]
    [Route("ErrorLog")]
    public class ErrorLogController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
