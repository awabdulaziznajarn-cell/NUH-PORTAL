using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة الأدوار والصلاحيات (MVC) — محميّة بصلاحية roles.view.
    // القشرة بس؛ البيانات من /api/Roles (كوكي MVC بيوثّق تلقائيًا).
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "roles.view")]
    [Route("Roles")]
    public class RolesController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
