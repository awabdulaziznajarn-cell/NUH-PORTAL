using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Core;
using NUH_PORTAL.Services;
using NUH_PORTAL.Services.Interfaces;
using NUH_PORTAL.ViewModels;

namespace NUH_PORTAL.Controllers
{
    // لوحة التحكم (MVC) — محمية بالكوكي، بتحقن الخدمات مباشرة وبترندر الأرقام من السيرفر فورًا
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [Route("Home")]
    public class HomeController : Controller
    {
        private readonly IStudentService _students;
        private readonly IStudentStatusService _status;
        private readonly IRequestService _requests;
        private readonly FacultyHousingService _faculty;

        public HomeController(IStudentService students, IStudentStatusService status,
                              IRequestService requests, FacultyHousingService faculty)
        {
            _students = students;
            _status = status;
            _requests = requests;
            _faculty = faculty;
        }

        // GET /Home
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var vm = new DashboardViewModel
            {
                Stats = await _students.GetStatsAsync(),
                StatusStats = await _status.GetStatsAsync(),
                LatestStudents = (await _students.GetPagedAsync(
                    new QueryParams { Page = 1, PageSize = 6 }, showDeleted: false, adStatus: null)).Items,
                LatestRequests = (await _requests.GetPagedAsync(
                    new QueryParams { Page = 1, PageSize = 6 }, status: null, requestType: null)).Items
            };

            // ⚠️ الاستعلام مشروط بالصلاحية لا الشاشة: لو اتحسب دايمًا وخبّيناه
            //    في الـ view، كنا بندفع أربع استعلامات مع كل فتح للوحة لمستخدم
            //    مش هيشوفها - والأرقام بتوصل الصفحة أصلًا حتى لو مخفية.
            if (User.HasClaim(ClaimConstants.Permission, "facultyHousing.view"))
            {
                vm.FacultyStats = await _faculty.GetDashboardStatsAsync();
            }

            return View(vm);
        }
    }
}
