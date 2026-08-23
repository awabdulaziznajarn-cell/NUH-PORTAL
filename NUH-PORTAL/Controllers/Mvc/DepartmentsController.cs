using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة إدارة الأقسام (MVC) — مستقلة. البيانات من /api/admin/departments.
    //
    // ⚠️ هذا الملف كان مفقودًا وحده من بين شاشات القوائم المرجعية الخمس، بينما
    //    Views/Departments/Index.cshtml موجود ورابط «الأقسام» في القائمة الجانبية
    //    يشير إليه — فكان الضغط عليه يعطي 404 من غير أثر في سجل الأخطاء، لأن
    //    الطلب لا يصل إلى الشاشة أصلًا. الأرجح أنه حُذف مع DepartureController.cs
    //    لتجاور الاسمين في مستكشف المشروع.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "lookups.manage")]
    [Route("Departments")]
    public class DepartmentsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
