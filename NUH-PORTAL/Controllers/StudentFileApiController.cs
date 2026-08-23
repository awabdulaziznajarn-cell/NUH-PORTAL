using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // ========================================================================
    //  ملف الطالب المجمّع - شاشة الأمن السيبراني.
    //
    //  ⚠️ صلاحية مستقلة (students.investigate) مش students.view: الشاشة دي
    //     بتجمع بيانات الطالب وطلبه وتعهّده وحساب شبكته في رد واحد - الشرح في
    //     Core/ApplicationPermissions.cs.
    //
    //  ⚠️ واسم الكلاس فيه Api عن قصد: في كنترولر MVC باسم StudentFile بيقدّم
    //     الشاشة نفسها، والاسمين لازم يفضلوا مختلفين عشان ما يبقاش في لبس في
    //     تحديد الكنترولر ولا في مسار الـ View.
    // ========================================================================
    [Authorize(Policy = "students.investigate")]
    [Route("api/StudentFile")]
    [ApiController]
    public class StudentFileApiController : ControllerBase
    {
        private readonly IStudentFileService _service;

        public StudentFileApiController(IStudentFileService service) => _service = service;

        // GET /api/StudentFile?q=487799810&requestId=22
        [HttpGet("")]
        public async Task<IActionResult> Get([FromQuery] string q, [FromQuery] int? requestId = null)
            => Ok(await _service.GetAsync(q, requestId));
    }
}
