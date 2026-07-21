using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Services.Interfaces;
using NUH_PORTAL.ViewModels;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة قائمة الطلاب (MVC) — بتحقن IStudentService مباشرة، نفس خدمات الـ API بالظبط
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [Route("Students")]
    public class StudentsController : Controller
    {
        private readonly IStudentService _students;

        public StudentsController(IStudentService students) => _students = students;

        // GET /Students
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var vm = new StudentsIndexViewModel
            {
                Stats = await _students.GetStatsAsync(),
                Page = await _students.GetPagedAsync(new QueryParams { Page = 1, PageSize = 20 }, showDeleted: false, adStatus: null)
            };
            return View(vm);
        }
    }
}
