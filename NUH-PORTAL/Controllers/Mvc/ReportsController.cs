using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // مركز التقارير (MVC) — بيعيد استخدام منطق صفحة التقارير القديمة كما هو (reports-page.js)
    // الوصول للصفحة بالصلاحية مش بالدور — عشان أي دور جديد ياخد الصلاحية ويشتغل
    // من غير ما نعدّل الكود. الشاشة نفسها بتختفي من القائمة الجانبية كمان.
    // ⚠️ reports.page لا reports.view: كل بيانات الشاشة من AuditLogsController،
    //    فبدون auditLogs.view كانت بتفتح نصّها فاضي. التعريف في Program.cs.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "reports.page")]
    [Route("Reports")]
    public class ReportsController : Controller
    {
        // GET /Reports
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
