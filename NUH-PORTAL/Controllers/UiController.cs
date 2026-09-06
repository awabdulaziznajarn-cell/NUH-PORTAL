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
        private readonly IConfiguration _config;
        private readonly Microsoft.Extensions.Localization.IStringLocalizer<SharedResource> _t;

        public UiController(
            IUiBootstrapService boot,
            IConfiguration config,
            Microsoft.Extensions.Localization.IStringLocalizer<SharedResource> t)
        {
            _boot = boot;
            _config = config;
            _t = t;
        }

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

        // ====================================================================
        //  GET /js/nuh-housing.js — بنية السكن: الأدوار والشقق والغرف وترقيمها.
        //
        //  ⚠️ نفس منطق nuh-id.js: القاعدة في Core/HousingStructure.cs والمتصفح
        //     بياخدها متولّدة منها. قبل كده كانت مكتوبة في
        //     wwwroot/js/housing-fields.js وحده - يعني في المتصفح بس، والخادم
        //     مكانش يعرف إن المبنى فيه ٢٠ شقة فكان بيقبل أي رقم يوصله.
        //     الحارس الوحيد كان القائمة المنسدلة، وده تحقّق شكلي أي حد يعدّيه.
        //
        //  ⚠️ AllowAnonymous لأن فورم تسجيل الطالب صفحة عامة بتحمّله.
        // ====================================================================
        [AllowAnonymous]
        [HttpGet("/js/nuh-housing.js")]
        [Produces("application/javascript")]
        public IActionResult HousingStructureScript() => GeneratedScript(Core.HousingStructure.ToJavaScript());

        // ====================================================================
        //  GET /js/nuh-trial.js — شارة «تشغيل تجريبي» لصفحات البوابة العامة.
        //
        //  ⚠️ ليه ملف متولّد لا سطر HTML في كل صفحة:
        //     شاشات الموظفين بتترندر من السيرفر فـ _Layout بيقرا الإعداد
        //     بنفسه. لكن الصفحة الرئيسية وبوابة الطالب صفحات HTML **ثابتة** -
        //     مابتعرفش تقرا appsettings. لو كتبنا الشارة فيها بالإيد، كان يوم
        //     الإطلاق لازم نفتكر نشيلها من تسع صفحات، والصفحة اللي تتنسي
        //     هتفضل مكتوب عليها «تشغيل تجريبي» بعد الإطلاق بشهور.
        //
        //     كده الظهور من إعداد واحد (Trial:Enabled) والنصّ من ملف الترجمة
        //     الواحد (ui_trialMode) - نفس منطق nuh-id.js و nuh-pledge.js.
        //
        //  ⚠️ لو الوضع مقفول بيرجّع سكربت فاضي لا 404: الصفحة مالهاش دخل،
        //     وطلب بيرجع 404 في كونسول كل زائر بيبان كأنه عطل.
        //
        //  ⚠️ position:fixed لا في ترويسة الصفحة: الصفحات العامة ترويساتها
        //     مختلفة (الرئيسية فيها شريط، صفحة الدخول مفيهاش)، والمطلوب إن
        //     الشارة تبقى في **نفس المكان** في كل شاشة.
        //
        //  ⚠️ والطباعة بتخفيها: الوثائق الرسمية بتخرج نضيفة منها.
        // ====================================================================
        [AllowAnonymous]
        [HttpGet("/js/nuh-trial.js")]
        [Produces("application/javascript")]
        public IActionResult TrialBadgeScript([FromQuery] string? lang = null)
        {
            // ⚠️ اللغة من الاستعلام زي /api/ui/i18n: صفحات البوابة العامة
            //    مابتعدّيش على كوكي الثقافة، ولغتها محفوظة في المتصفح عندها.
            if (lang != null)
            {
                var ci = new CultureInfo(lang == "en" ? "en" : "ar");
                CultureInfo.CurrentCulture = ci;
                CultureInfo.CurrentUICulture = ci;
            }

            if (!_config.GetValue<bool>("Trial:Enabled"))
                return GeneratedScript("/* Trial:Enabled = false */\n");

            var ar = System.Text.Json.JsonSerializer.Serialize(_t["ui_trialMode"].Value);

            var js =
                "(function(){'use strict';\n" +
                "  if (document.getElementById('nuh-trial')) return;\n" +
                "  var css = '#nuh-trial{position:fixed;top:12px;inset-inline-end:16px;z-index:9000;'\n" +
                "    + 'font-family:inherit;font-size:11.5px;font-weight:800;line-height:1.6;'\n" +
                "    + 'padding:4px 12px;border-radius:99px;white-space:nowrap;pointer-events:none;'\n" +
                "    + 'background:#fffaeb;color:#93370d;border:1px solid #f0dfa4;'\n" +
                "    + 'box-shadow:0 1px 3px rgba(16,24,40,.08)}'\n" +
                "    + '@media print{#nuh-trial{display:none !important}}';\n" +
                "  var s = document.createElement('style'); s.textContent = css;\n" +
                "  document.head.appendChild(s);\n" +
                "  function add(){\n" +
                "    if (document.getElementById('nuh-trial')) return;\n" +
                "    var b = document.createElement('div');\n" +
                "    b.id = 'nuh-trial'; b.textContent = " + ar + ";\n" +
                "    document.body.appendChild(b);\n" +
                "  }\n" +
                "  if (document.body) add();\n" +
                "  else document.addEventListener('DOMContentLoaded', add);\n" +
                "})();\n";

            return GeneratedScript(js);
        }

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
