using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.AuditLogs;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — استعلامات وتقارير السجل في IAuditLogQueryService
    //
    // ⚠️ الصلاحية على الكلاس لا على كل دالة. كانت مكتوبة على أربع دوال
    //    وسايبة تلاتة بـ [Authorize] وحدها — يعني أي حساب مسجّل دخول، بما
    //    فيهم حساب طالب، كان يقرا chart-data و alerts و today-stats: عدد
    //    محاولات الدخول الفاشلة النهارده وإنذار «نشاط تسجيل دخول مشبوه».
    //    يعني اللي بيحاول يخمّن كلمة سر كان يقدر يسأل النظام: هل اتنبهتوا؟
    //
    //    ولمّا بقت على الكلاس، أي دالة تتضاف هنا بعدين بتاخدها بالوراثة —
    //    فالنسيان نفسه بقى مستحيل، مش مجرد متصلَّح مرة.
    [Authorize(Policy = "auditLogs.view")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogQueryService _service;

        public AuditLogsController(IAuditLogQueryService service) => _service = service;

        // GET api/AuditLogs
        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] int? userId = null,
            [FromQuery] string? actionGroup = null,
            [FromQuery] string? action = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortAsc = false,
            [FromQuery] int? facultyUnitId = null)
            => Ok(await _service.GetLogsAsync(page, pageSize, new AuditLogFilter
            {
                UserId = userId,
                ActionGroup = actionGroup,
                Action = action,
                FromDate = fromDate,
                ToDate = toDate,
                Search = search,
                SortBy = sortBy,
                SortAsc = sortAsc,
                FacultyUnitId = facultyUnitId
            }));

        // GET api/AuditLogs/export
        [HttpGet("export")]
        public async Task<IActionResult> ExportLogs(
            [FromQuery] int? userId = null,
            [FromQuery] string? actionGroup = null,
            [FromQuery] string? action = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? search = null,
            [FromQuery] int? facultyUnitId = null,
            // ⚠️ تقويم العرض جاي من الشاشة عشان الملف يطلع بنفس التقويم اللي
            //    الموظف شايفه. التخزين والفلترة ميلادي دايمًا.
            [FromQuery] string? calendar = null)
        {
            var file = await _service.ExportLogsAsync(new AuditLogFilter
            {
                UserId = userId,
                ActionGroup = actionGroup,
                Action = action,
                FromDate = fromDate,
                ToDate = toDate,
                Search = search,
                FacultyUnitId = facultyUnitId
            }, calendar);
            return File(file.Content, file.ContentType, file.FileName);
        }

        // GET api/AuditLogs/chart-data
        [HttpGet("chart-data")]
        public async Task<IActionResult> GetChartData(
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
            => Ok(await _service.GetChartDataAsync(fromDate, toDate));

        // GET api/AuditLogs/alerts
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
            => Ok(await _service.GetAlertsAsync());

        // GET api/AuditLogs/report-html
        [HttpGet("report-html")]
        public async Task<IActionResult> GetReportHtml(
            [FromQuery] string? type = "activity",
            [FromQuery] int? userId = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? calendar = null)
        {
            var lang = Request.Headers["Accept-Language"].ToString().StartsWith("ar") ? "ar" : "en";
            var html = await _service.GetReportHtmlAsync(type, userId, fromDate, toDate, lang, calendar);
            return Content(html, "text/html;charset=utf-8");
        }

        // GET api/AuditLogs/today-stats
        [HttpGet("today-stats")]
        public async Task<IActionResult> GetTodayStats(
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
            => Ok(await _service.GetTodayStatsAsync(fromDate, toDate));

        // GET api/AuditLogs/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
            => Ok(await _service.GetUsersAsync());
    }
}
