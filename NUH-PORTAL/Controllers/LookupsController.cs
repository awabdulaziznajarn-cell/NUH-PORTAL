using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // endpoints القوائم المنسدلة — بيانات مرجعية غير حسّاسة، متاحة للفورم
    [ApiController]
    [Route("api/lookups")]
    public class LookupsController : ControllerBase
    {
        private readonly ILookupService _svc;
        public LookupsController(ILookupService svc) => _svc = svc;

        // ====================================================================
        //  ⚠️ لغة أسماء القوائم:
        //     LookupService بيختار ArName أو EnName حسب CultureInfo.CurrentCulture،
        //     والثقافة دي بتتحدّد من كوكي .AspNetCore.Culture اللي بيتكتب من شاشة
        //     تبديل اللغة بتاعة الموظفين (/Culture/Set).
        //
        //     بوابة الطالب صفحات HTML ثابتة ولغتها محفوظة في متصفحه، فالكوكي ده
        //     مش موجود عنده أصلًا — يعني أسماء الكليات والأقسام والمباني كانت
        //     بترجع عربي دايمًا حتى لما الطالب مختار إنجليزي، فالنموذج يطلع
        //     نصّه إنجليزي وقوائمه عربي.
        //
        //     الـ lang هنا بيتقرا من الرابط ويخصّ الطلب الحالي بس (lookups.js
        //     بيضيفه تلقائيًا). لو مش موجود بيفضل السلوك القديم زي ما هو.
        // ====================================================================
        private static void UseLang(string? lang)
        {
            if (string.IsNullOrEmpty(lang)) return;
            var ci = new CultureInfo(lang == "en" ? "en" : "ar");
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;
        }

        [HttpGet("colleges"), AllowAnonymous]
        public async Task<IActionResult> Colleges([FromQuery] string? lang = null)
        {
            UseLang(lang);
            return Ok(await _svc.GetCollegesAsync());
        }

        [HttpGet("departments"), AllowAnonymous]
        public async Task<IActionResult> Departments([FromQuery] int? collegeId, [FromQuery] string? lang = null)
        {
            UseLang(lang);
            return Ok(await _svc.GetDepartmentsAsync(collegeId));
        }

        [HttpGet("buildings"), AllowAnonymous]
        public async Task<IActionResult> Buildings([FromQuery] Gender? gender, [FromQuery] string? lang = null)
        {
            UseLang(lang);
            return Ok(await _svc.GetBuildingsAsync(gender));
        }

        [HttpGet("academic-levels"), AllowAnonymous]
        public async Task<IActionResult> AcademicLevels([FromQuery] string? lang = null)
        {
            UseLang(lang);
            return Ok(await _svc.GetAcademicLevelsAsync());
        }

        [HttpGet("terms"), AllowAnonymous]
        public async Task<IActionResult> Terms([FromQuery] string? lang = null)
        {
            UseLang(lang);
            return Ok(await _svc.GetTermsAsync());
        }
    }
}
