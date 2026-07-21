using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers
{
    // صفحات الـ MVC محمية بالكوكي — مش داخل؟ بتتحول تلقائيًا لـ /Account/Login
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [Route("Home")]
    public class HomeController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
