using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة «ملف الطالب» (MVC) - البيانات من /api/StudentFile.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "students.investigate")]
    [Route("StudentFile")]
    public class StudentFileController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
