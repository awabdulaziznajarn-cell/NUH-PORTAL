using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة سجل العمليات (MVC) — الجدول والفلاتر والتصدير على نفس /api/auditlogs
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "auditLogs.view")]
    [Route("AuditLog")]
    public class AuditLogController : Controller
    {
        // GET /AuditLog
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
