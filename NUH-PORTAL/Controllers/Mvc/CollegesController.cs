using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة إدارة الكليات (MVC) — مستقلة. البيانات من /api/admin/colleges.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "lookups.manage")]
    [Route("Colleges")]
    public class CollegesController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
