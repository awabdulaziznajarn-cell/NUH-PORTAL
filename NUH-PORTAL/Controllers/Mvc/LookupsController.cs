using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة إدارة القوائم المرجعية (MVC) — للأدمن فقط.
    // الصفحة بترندر القشرة فقط؛ البيانات بتتحمّل بالـ JS من /api/admin (كوكي MVC بيوثّق تلقائيًا).
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "admin")]
    [Route("Lookups")]
    public class LookupsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
