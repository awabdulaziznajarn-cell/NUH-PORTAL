using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة سجل الدخول والخروج (MVC) — محميّة بصلاحية signInLog.view. البيانات من /api/logs/signins.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "signInLog.view")]
    [Route("SignInLog")]
    public class SignInLogController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
