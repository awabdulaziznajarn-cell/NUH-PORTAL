using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة إدارة بنود التعهّد (MVC) — مستقلة. البيانات من /api/admin/pledge-terms.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "lookups.manage")]
    [Route("Terms")]
    public class TermsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
