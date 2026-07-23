using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة إدارة المباني (MVC) — مستقلة. البيانات من /api/admin/buildings.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "lookups.manage")]
    [Route("Buildings")]
    public class BuildingsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
