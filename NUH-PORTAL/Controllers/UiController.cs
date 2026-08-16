using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // ========================================================================
    //  قاموس الترجمة لصفحات بوابة الطالب.
    //
    //  ⚠️ ليه النقطة دي موجودة:
    //     شاشات الموظفين بتترندر من السيرفر، فـ _Layout.cshtml بيحقن القاموس
    //     كامل جوه <script> ومحدش بيحتاج يجيبه. لكن بوابة الطالب صفحات HTML
    //     ثابتة (wwwroot/register/*.html) — مبتعدّيش على أي لياوت، فـ
    //     window.__I18N كان مش موجود عندها خالص. النتيجة اللي كانت ظاهرة
    //     للطالب: زرار «English» في البوابة كان بيقلب اتجاه الصفحة بس،
    //     وكل النصوص تفضل عربي.
    //
    //     الحل إن الصفحة تجيب نفس القاموس من نفس ملفات الـ resx. يعني النص
    //     مكتوب مرة واحدة في Resources/SharedResource*.resx وبيخدم شاشة
    //     الموظف وصفحة الطالب — مش نسختين تتفرّقوا مع أول تعديل.
    // ========================================================================
    [ApiController]
    [Route("api/ui")]
    public class UiController : ControllerBase
    {
        private readonly IUiBootstrapService _boot;

        public UiController(IUiBootstrapService boot) => _boot = boot;

        // GET /api/ui/i18n?lang=ar|en
        // متاحة بلا مصادقة عن قصد: صفحة تسجيل الطالب نفسها متاحة قبل الدخول،
        // والمحتوى نصوص واجهة مفلترة على مفاتيح البوابة — مفيش بيانات ولا
        // نصوص شاشات إدارية.
        [AllowAnonymous]
        [HttpGet("i18n")]
        public async Task<IActionResult> I18n([FromQuery] string? lang = null)
        {
            // ⚠️ الثقافة هنا بتتحدّد من الاستعلام مش من كوكي الثقافة: الطالب
            //    مابيعدّيش على /Culture/Set (ده لشاشات الموظفين)، ولغته محفوظة
            //    في المتصفح عنده. الضبط ده بيخصّ الطلب الحالي بس.
            var ci = new CultureInfo(lang == "en" ? "en" : "ar");
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;

            var json = await _boot.GetPortalBootstrapJsonAsync();

            // القاموس بيتغيّر مع النشر بس (والقوائم كل دقيقة) — نخلّي المتصفح
            // يخزّنه بدل ما يجيبه مع كل صفحة في البوابة.
            Response.Headers["Cache-Control"] = "public, max-age=300";
            return Content(json, "application/json; charset=utf-8");
        }
    }
}
