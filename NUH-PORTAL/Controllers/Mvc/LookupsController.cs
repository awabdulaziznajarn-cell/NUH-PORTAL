using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // الشاشة القديمة الموحّدة (تابات) اتفصلت لشاشات مستقلة: /Colleges /Departments /Buildings
    // /AcademicLevels /Terms. بنحوّل الرابط القديم للكليات عشان أي bookmark/redirect قديم يفضل شغّال.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "lookups.manage")]
    [Route("Lookups")]
    public class LookupsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => Redirect("/Colleges");
    }
}
