using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.AuditLogs;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — استعلامات وتقارير السجل في IAuditLogQueryService
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogQueryService _service;

        public AuditLogsController(IAuditLogQueryService service) => _service = service;

        // GET api/AuditLogs
        [Authorize(Policy = "auditLogs.view")]
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
            [FromQuery] bool sortAsc = false)
            => Ok(await _service.GetLogsAsync(page, pageSize, new AuditLogFilter
            {
                UserId = userId,
                ActionGroup = actionGroup,
                Action = action,
                FromDate = fromDate,
                ToDate = toDate,
                Search = search,
                SortBy = sortBy,
                SortAsc = sortAsc
            }));

        // GET api/AuditLogs/export
        [Authorize(Policy = "auditLogs.view")]
        [HttpGet("export")]
        public async Task<IActionResult> ExportLogs(
            [FromQuery] int? userId = null,
            [FromQuery] string? actionGroup = null,
            [FromQuery] string? action = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? search = null)
        {
            var file = await _service.ExportLogsAsync(new AuditLogFilter
            {
                UserId = userId,
                ActionGroup = actionGroup,
                Action = action,
                FromDate = fromDate,
                ToDate = toDate,
                Search = search
            });
            return File(file.Content, file.ContentType, file.FileName);
        }

        // GET api/AuditLogs/chart-data
        [HttpGet("chart-data")]
        public async Task<IActionResult> GetChartData()
            => Ok(await _service.GetChartDataAsync());

        // GET api/AuditLogs/alerts
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
            => Ok(await _service.GetAlertsAsync());

        // GET api/AuditLogs/report-html
        [Authorize(Policy = "auditLogs.view")]
        [HttpGet("report-html")]
        public async Task<IActionResult> GetReportHtml(
            [FromQuery] string? type = "activity",
            [FromQuery] int? userId = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
        {
            var lang = Request.Headers["Accept-Language"].ToString().StartsWith("ar") ? "ar" : "en";
            var html = await _service.GetReportHtmlAsync(type, userId, fromDate, toDate, lang);
            return Content(html, "text/html;charset=utf-8");
        }

        // GET api/AuditLogs/today-stats
        [HttpGet("today-stats")]
        public async Task<IActionResult> GetTodayStats()
            => Ok(await _service.GetTodayStatsAsync());

        // GET api/AuditLogs/users
        [Authorize(Policy = "auditLogs.view")]
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
            => Ok(await _service.GetUsersAsync());
    }
}
