using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // استعلامات سجلّي الأخطاء والدخول — كل سجل محمي بصلاحيته.
    [Authorize]
    [Route("api/logs")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly ILogQueryService _service;

        public LogsController(ILogQueryService service) => _service = service;

        // GET api/logs/errors
        [HttpGet("errors")]
        [Authorize(Policy = "errorLogs.view")]
        public async Task<IActionResult> GetErrors(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortAsc = false)
            => Ok(await _service.GetErrorLogsAsync(page, pageSize, search, fromDate, toDate, sortBy, sortAsc));

        // GET api/logs/errors/{id}
        [HttpGet("errors/{id:int}")]
        [Authorize(Policy = "errorLogs.view")]
        public async Task<IActionResult> GetError(int id)
            => Ok(await _service.GetErrorLogAsync(id));

        // GET api/logs/signins
        [HttpGet("signins")]
        [Authorize(Policy = "signInLog.view")]
        public async Task<IActionResult> GetSignIns(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? eventType = null,
            [FromQuery] string? search = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortAsc = false)
            => Ok(await _service.GetSignInLogsAsync(page, pageSize, eventType, search, fromDate, toDate, sortBy, sortAsc));
    }
}
