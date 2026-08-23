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
        public async Task<IActionResult> GetRecent([FromQuery] int skip = 0)
            => Ok(await _service.GetRecentAsync(skip));

        // GET api/student-status/{studentId}
        [HttpGet("{studentId}")]
        public async Task<IActionResult> GetStudentHistory(int studentId)
            => Ok(await _service.GetStudentHistoryAsync(studentId));

        // GET api/student-status/{actionId}/attachment[?download=true]
        // ⚠️ الشاشة كانت بتبني الرابط ده وتعرضه للموظف (شاشة «تحديث حالة
        //    الطالب»)، والمسار مش موجود أصلًا في الكنترولر — فأي ضغطة على
        //    مرفق كانت بترجّع 404. الخدمة GetActionAttachmentAsync والواجهة
        //    كانوا موجودين ومكتملين؛ الناقص كان السطر ده وحده.
        //    من غير download بيرجع inline — الصور و PDF بتتعرض في المتصفح
        //    بدل ما تتنزّل، نفس سلوك مرفق نقل السكن بالحرف.
        // ⚠️ ممنوع تخزينه في أي كاش. الرد ده نتيجة *قرار صلاحية* يخصّ
        //    المستخدم الحالي، والمتصفح بيتعامل معاه كملف عادي فبيخزّنه
        //    بالرابط — فنفس الرابط بيتفتح تاني من الكاش من غير ما يوصل
        //    للسيرفر أصلًا، فالفحص ما بيتنفّذش. ده مش سيناريو نظري:
        //    كان بيخلّي اختبار «مشرف القسم التاني» يبان ناجح وهو مش ناجح.
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [HttpGet("{actionId}/attachment")]
        public async Task<IActionResult> GetAttachment(int actionId, [FromQuery] bool download = false)
        {
            var file = await _service.GetActionAttachmentAsync(actionId);
            return download
                ? PhysicalFile(file.FilePath, file.ContentType, file.OriginalFileName)
                : PhysicalFile(file.FilePath, file.ContentType);
        }
    }
}
