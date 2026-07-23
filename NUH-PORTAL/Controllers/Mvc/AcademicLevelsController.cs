using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة إدارة المستويات الدراسية (MVC) — مستقلة. البيانات من /api/admin/academic-levels.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "lookups.manage")]
    [Route("AcademicLevels")]
    public class AcademicLevelsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
