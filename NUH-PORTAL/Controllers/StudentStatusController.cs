using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق حالات المغادرة في IStudentStatusService
    // القراءة students.view، وتسجيل الحالة students.changeStatus — مش لتوكن الطالب (OTP).
    // ⚠️ كان اسمًا مش معرّف في ApplicationPermissions، فتسجيل التخرّج/الفصل/التحويل
    //    كان بيرجّع 500. والاسم الصح مستخدم أصلًا جوّه الخدمة نفسها.
    [Authorize(Policy = "students.view")]
    [Route("api/student-status")]
    [ApiController]
    public class StudentStatusController : ControllerBase
    {
        private readonly IStudentStatusService _service;

        public StudentStatusController(IStudentStatusService service) => _service = service;

        // POST api/student-status  (multipart: studentNumber, statusType, notes, file?)
        [Authorize(Policy = "students.changeStatus")]
        [HttpPost]
        [RequestSizeLimit(Services.StudentStatusService.MaxFileSize)]
        public async Task<IActionResult> CreateStatusAction(
            [FromForm] string studentNumber,
            [FromForm] string statusType,
            [FromForm] string notes,
            IFormFile? file)
            => Ok(await _service.CreateStatusActionAsync(studentNumber, statusType, notes, file));

        // GET api/student-status/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
            => Ok(await _service.GetStatsAsync());

        // GET api/student-status/recent
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
            => Ok(await _service.GetRecentAsync());

        // GET api/student-status/{studentId}
        [HttpGet("{studentId}")]
        public async Task<IActionResult> GetStudentHistory(int studentId)
            => Ok(await _service.GetStudentHistoryAsync(studentId));
    }
}
