using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة إدارة المستخدمين (MVC) — محميّة بصلاحية users.view.
    // القشرة بس؛ البيانات بتتحمّل بالـ JS من /api/Users (كوكي MVC بيوثّق تلقائيًا).
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "users.view")]
    [Route("Users")]
    public class UsersController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
