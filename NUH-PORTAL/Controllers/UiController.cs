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

        // ====================================================================
        //  GET /js/nuh-id.js — قواعد الهوية والرقم الجامعي والجوال للمتصفح.
        //
        //  ⚠️ نفس منطق /api/ui/i18n بالظبط: القاعدة مكتوبة مرة واحدة في
        //     Core/IdentityRules.cs، والمتصفح بياخدها متولّدة منها. قبل كده كان
        //     في ملف ثابت wwwroot/js/identity-rules.js مكتوب بالإيد جنب ملف
        //     الـ C# — ونسختين لنفس القاعدة بيفضلوا متطابقين لحد أول تعديل
        //     مستعجل في واحد منهم.
        //
        //  ⚠️ المسار متكتب بشرطة في أوله عشان يتخطّى [Route("api/ui")]: العنوان
        //     لازم يفضل شبه ملف عادي، لأن بوابة الطالب صفحات HTML ثابتة
        //     بتنادي ../js/nuh-id.js وهي مش عارفة إن ده راوت.
        //
        //  ⚠️ [AllowAnonymous] لازمة: صفحة تسجيل الطالب متاحة قبل الدخول،
        //     والمحتوى أنماط تحقّق ورسائل خطأ — مفيش أي بيانات فيه.
        // ====================================================================
        [AllowAnonymous]
        [HttpGet("/js/nuh-id.js")]
        [Produces("application/javascript")]
        public IActionResult IdentityRulesScript() => GeneratedScript(Core.IdentityRules.ToJavaScript());

        // ====================================================================
        //  GET /js/nuh-pledge.js — جملة الإقرار وقاعدة مطابقتها.
        //
        //  ⚠️ نفس منطق nuh-id.js بالحرف: القاعدة في Core/PledgeRules.cs،
        //     والمتصفح بياخدها متولّدة منها. لو الجملة كانت مكتوبة في صفحة
        //     الإقرار وفي ملف الـ C#، أول تعديل في واحدة منهم كان هيخلّي كل
        //     طالب يكتب اللي على الشاشة والخادم يرفضه — وهو كاتب صح.
        // ====================================================================
        [AllowAnonymous]
        [HttpGet("/js/nuh-pledge.js")]
        [Produces("application/javascript")]
        public IActionResult PledgeRulesScript() => GeneratedScript(Core.PledgeRules.ToJavaScript());

        // ⚠️ الترويسات مكتوبة مرة واحدة للاتنين: النصّ مابيتغيّرش إلا مع نشر
        //    جديد، والـ ETag بيخلّي المتصفح يتأكد بطلب فاضي بدل ما يستنّى
        //    انتهاء المدة — فأي تعديل في القاعدة بيوصل من غير ما نستنّى كاش
        //    قديم يخلص.
        private IActionResult GeneratedScript(string js)
        {
            var etag = "\"" + Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(js)))[..16] + "\"";

            if (Request.Headers.IfNoneMatch.ToString() == etag)
                return StatusCode(StatusCodes.Status304NotModified);

            Response.Headers.ETag = etag;
            Response.Headers["Cache-Control"] = "public, max-age=300";
            return Content(js, "application/javascript; charset=utf-8");
        }
    }
}
