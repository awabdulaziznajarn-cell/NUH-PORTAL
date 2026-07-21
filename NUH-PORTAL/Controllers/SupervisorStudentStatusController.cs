using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — بيستخدم نفس IStudentStatusService بتاع الأدمن (مسار المشرف)
    [Authorize(Roles = "supervisor")]
    [Route("api/supervisor/student-status")]
    [ApiController]
    public class SupervisorStudentStatusController : ControllerBase
    {
        private readonly IStudentStatusService _service;

        public SupervisorStudentStatusController(IStudentStatusService service) => _service = service;

        // POST api/supervisor/student-status (multipart)
        [HttpPost]
        [RequestSizeLimit(Services.StudentStatusService.MaxFileSize)]
        public async Task<IActionResult> CreateStatusAction(
            [FromForm] string studentNumber,
            [FromForm] string statusType,
            [FromForm] string notes,
            IFormFile? file)
            => Ok(await _service.CreateSupervisorStatusActionAsync(studentNumber, statusType, notes, file));

        // GET api/supervisor/student-status/recent
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
            => Ok(await _service.GetRecentForSupervisorAsync());
    }
}
