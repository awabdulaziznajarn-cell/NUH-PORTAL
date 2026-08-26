using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // شاشة «وحدات سكن أعضاء هيئة التدريس» (MVC) — البيانات من /api/FacultyHousing.
    // ⚠️ نفس اسم كنترولر الـ API لكن في namespace تاني — ده نمط المشروع الموجود
    //    أصلًا (Controllers/RequestsController مع Controllers/Mvc/RequestsController)،
    //    والمسارات مختلفة فمافيش تعارض: api/FacultyHousing مقابل /FacultyHousing.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "facultyHousing.view")]
    [Route("FacultyHousing")]
    public class FacultyHousingController : Controller
    {
        [HttpGet("")]
        public IActionResult Index() => View();

        // ⚠️ شاشة مستقلة لا نافذة منبثقة: النموذج فيه ثلاثة أقسام ولوحة فرق،
        //    والنافذة المنبثقة تضغطه في مساحة لا تكفيه فتتداخل الحقول. وفوق
        //    ذلك للشاشة المستقلة رابط خاص بها، فيمكن فتحها ومشاركتها والرجوع
        //    منها - وهي أمور لا تتيحها النافذة.
        //
        // ⚠️ ومسار «Handover» بلا رقم وحدة اتشال ومعاه شاشة اختيار الوحدة
        //    (SelectUnit.cshtml و js/faculty-housing-select.js وبند القائمة
        //    الجانبية). كانت جدولًا تانيًا لنفس الـ٢٤٢ وحدة، بنفس البحث
        //    ونفس الفلاتر ونفس الترقيم، في ملفَّي عرض وملفَّي سكربت منفصلين -
        //    والفرق الوحيد زرّ الصفّ. وافترقت الشاشتان فعلًا: ترتيب أعمدتها
        //    اختلف عن القائمة لحد ما اتظبط بإيدنا.
        //    الإجراء دلوقتي في صفّ القائمة نفسه (زرّ «تغيير الساكن» / «تسكين»)
        //    جنب «تعديل» - والوحدة قدّامك وإنت بتختار.
        [HttpGet("Handover/{id:int}")]
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "facultyHousing.manage")]
        public IActionResult Handover(int id)
        {
            ViewData["Mode"] = "handover";
            return View(id);
        }

        // ⚠️ نفس العرض بوضع مختلف لا عرض ثانٍ: النموذجان يشتركان في حقول عضو
        //    هيئة التدريس كاملةً، ويختلفان في إظهار قسمين وفي نقطة الحفظ.
        //    نسخة ثانية من الملف كانت ستعني أن كل تعديل على الحقول يُنفَّذ
        //    مرتين - وأول مرة يُنسى فيها الثانية تفترق الشاشتان.
        [HttpGet("Edit/{id:int}")]
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "facultyHousing.manage")]
        public IActionResult Edit(int id)
        {
            ViewData["Mode"] = "edit";
            return View("Handover", id);
        }
    }
}
