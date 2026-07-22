using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة إدارة حسابات السكن (MVC) — admin فقط (نفس صلاحية API الـ HousingAccountManagement)
    // بتعيد استخدام housing-management.js القديم زي ما هو — نفس السلوك بالظبط بشكل موحّد
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "admin")]
    [Route("Housing")]
    public class HousingController : Controller
    {
        // GET /Housing
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
