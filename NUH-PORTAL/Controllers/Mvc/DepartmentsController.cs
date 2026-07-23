using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة إدارة الأقسام (MVC) — مستقلة. البيانات من /api/admin/departments.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "lookups.manage")]
    [Route("Departments")]
    public class DepartmentsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
