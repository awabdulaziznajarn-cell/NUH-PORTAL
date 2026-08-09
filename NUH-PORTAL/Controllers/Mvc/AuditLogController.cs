using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // ========================================================================
    //  صفحة سجل العمليات (MVC) — الجدول والفلاتر والتصدير على /api/auditlogs.
    //
    //  ⚠️ اتجرّب هنا تجهيز أول صفحة مع الـ HTML (زي RequestsController) عشان
    //     نوفّر رحلة ذهاب وإياب. والتجربة كشفت حاجة أهم: الصفحة نفسها بقت
    //     تتعلّق ٢٦ ثانية. يعني البطء **مش** في رحلة الشبكة الزيادة — البطء في
    //     استعلام سجل العمليات نفسه، وتجهيزه هنا نقل الانتظار من النداء للصفحة.
    //
    //     فرجعناها قشرة: الصفحة تفتح فورًا، والانتظار يفضل في النداء لحد ما
    //     يتصلّح الاستعلام. تجهيز أول صفحة يرجع بعد كده — لكنه تحسين للرحلة
    //     مش علاج للاستعلام، والترتيب الصح إن الاستعلام يتصلّح الأول.
    // ========================================================================
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "auditLogs.view")]
    [Route("AuditLog")]
    public class AuditLogController : Controller
    {
        // GET /AuditLog
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
