using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Services.Interfaces;
using NUH_PORTAL.ViewModels;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة قائمة الطلاب (MVC) — بتحقن IStudentService مباشرة، نفس خدمات الـ API بالظبط
    // الوصول للصفحة بالصلاحية مش بالدور — عشان أي دور جديد ياخد الصلاحية ويشتغل
    // من غير ما نعدّل الكود. الشاشة نفسها بتختفي من القائمة الجانبية كمان.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "students.view")]
    [Route("Students")]
    public class StudentsController : Controller
    {
        private readonly IStudentService _students;
        private readonly NUH_PORTAL.Data.Interfaces.IUnitOfWork _uow;

        public StudentsController(IStudentService students, NUH_PORTAL.Data.Interfaces.IUnitOfWork uow)
        {
            _students = students;
            _uow = uow;
        }

        // GET /Students
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var vm = new StudentsIndexViewModel
            {
                Stats = await _students.GetStatsAsync(),
                Page = await _students.GetPagedAsync(new QueryParams { Page = 1, PageSize = 20 }, showDeleted: false, adStatus: null),
                Scope = _uow.GetGenderScope()
            };
            return View(vm);
        }
    }
}
