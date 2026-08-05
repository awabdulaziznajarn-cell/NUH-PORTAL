using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع: بيوجّه للـ IStudentService بس. كل المنطق + التحقق + الصلاحيات + الـ audit جوه الـ service.
    // الأخطاء بترمى كـ UserFriendlyException وبيترجمها ExceptionHandlingMiddleware لـ JSON { message } بالـ status الصح.
    // الصلاحيات: القراءة students.view، الإضافة students.create، التعديل students.edit،
    // والحذف/الاستعادة students.delete — كل إجراء له صلاحيته عشان المسؤول يقدر
    // يدّي موظف حق التعديل من غير ما يدّيه حق الحذف.
    [Authorize(Policy = "students.view")]
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly IStudentService _service;

        public StudentsController(IStudentService service) => _service = service;

        // GET api/Students?showDeleted=&adStatus=
        [HttpGet]
        public async Task<IActionResult> GetStudents([FromQuery] bool showDeleted = false, [FromQuery] string? adStatus = null)
            => Ok(await _service.GetStudentsAsync(showDeleted, adStatus));

        // GET api/Students/paged?page=&pageSize=&filterText=&sortBy=&sortAsc=&showDeleted=&adStatus=
        [HttpGet("paged")]
        public async Task<IActionResult> GetStudentsPaged([FromQuery] QueryParams queryParams, [FromQuery] bool showDeleted = false, [FromQuery] string? adStatus = null)
            => Ok(await _service.GetPagedAsync(queryParams, showDeleted, adStatus));

        // GET api/Students/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
            => Ok(await _service.GetStatsAsync());

        // GET api/Students/by-number/{studentNumber}
        // بحث بالرقم الجامعي — بديل تحميل قائمة الطلاب كاملة في المتصفح.
        // بيرجّع 404 لو مش موجود عشان الواجهة تعرض رسالة واضحة.
        [HttpGet("by-number/{studentNumber}")]
        public async Task<IActionResult> GetStudentByNumber(string studentNumber)
        {
            var student = await _service.GetByStudentNumberAsync(studentNumber);
            return student == null ? NotFound(new { message = "الطالب غير موجود" }) : Ok(student);
        }

        // GET api/Students/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudent(int id)
            => Ok(await _service.GetByIdAsync(id));

        // POST api/Students
        [Authorize(Policy = "students.create")]
        [HttpPost]
        public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetStudent), new { id = created.Id }, created);
        }

        // PUT api/Students/{id}
        [Authorize(Policy = "students.edit")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentUpdateDto dto)
            => Ok(await _service.UpdateAsync(id, dto));

        // DELETE api/Students/{id}
        [Authorize(Policy = "students.delete")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteStudent(int id)
        {
            await _service.DeleteAsync(id);
            return Ok(new { message = "تم حذف الطالب بنجاح", messageEn = "Student deleted successfully", deleted = true });
        }

        // GET api/Students/{id}/lifecycle
        [HttpGet("{id}/lifecycle")]
        public async Task<IActionResult> GetLifecycleLogs(int id)
            => Ok(new { logs = await _service.GetLifecycleAsync(id) });

        // POST api/Students/{id}/restore
        [Authorize(Policy = "students.delete")]
        [HttpPost("{id}/restore")]
        public async Task<IActionResult> RestoreStudent(int id)
        {
            await _service.RestoreAsync(id);
            return Ok(new { message = "تم استعادة الطالب بنجاح", messageEn = "Student restored successfully", restored = true });
        }
    }
}
