using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة تحديث حالة الطالب الخاصة بالمشرف (MVC) — نفس منطق supervisor-departure.html
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [Route("Departure")]
    public class DepartureController : Controller
    {
        // GET /Departure
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
