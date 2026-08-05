using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة تحديث حالة الطالب (MVC) — الإحصائيات من السيرفر،
    // والفورمين (الحالة الأكاديمية + نقل السكن) بيشتغلوا على نفس الـ APIs القديمة
    // الوصول للصفحة بالصلاحية مش بالدور — عشان أي دور جديد ياخد الصلاحية ويشتغل
    // من غير ما نعدّل الكود. الشاشة نفسها بتختفي من القائمة الجانبية كمان.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "students.changeStatus")]
    [Route("StudentStatus")]
    public class StudentStatusController : Controller
    {
        private readonly IStudentStatusService _status;

        public StudentStatusController(IStudentStatusService status) => _status = status;

        // GET /StudentStatus
        [HttpGet("")]
        public async Task<IActionResult> Index()
            => View(await _status.GetStatsAsync());
    }
}
