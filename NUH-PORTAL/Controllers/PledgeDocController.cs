using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // ========================================================================
    //  وثيقة التعهّد المطبوعة - تسجيل الطباعة وتاريخها.
    //
    //  ⚠️ ليه مسار على الخادم أصلًا والطباعة كلها في المتصفح:
    //     الورقة دي بتتطبع وقت التحقيق مع الطالب وبتدخل المحضر. حاجتين
    //     كانوا ناقصين وهما جوهر أي ورقة رسمية:
    //       • تاريخ الطباعة كان من `new Date()` - ساعة جهاز اللي بيطبع، والنظام
    //         مش شاهد عليها.
    //       • مفيش أي أثر إن الوثيقة اتطبعت. فتح ملف الطالب متسجّل
    //         (student_file_viewed) والطباعة لأ - فسؤال «مين طبع النسخة دي؟»
    //         ما كانش ليه إجابة.
    //     النداء الواحد ده بيعمل الاتنين معًا.
    //
    //  ⚠️ السياسة pledge.print مركّبة في Program.cs لا صلاحية جديدة: هي بالظبط
    //     «اللي بيقدر يفتح شاشة فيها الوثيقة» - شاشة الطلب أو ملف الطالب.
    //     صلاحية جديدة كانت هتحتاج إسنادًا يدويًا لكل دور وتُنسى مع أول دور
    //     جديد، والنتيجة موظف بيطبع وثيقة والطباعة مش بتتسجّل.
    // ========================================================================
    [ApiController]
    [Route("api/PledgeDoc")]
    [Authorize(Policy = "pledge.print")]
    public class PledgeDocController : ControllerBase
    {
        private readonly IPledgeService _pledge;

        public PledgeDocController(IPledgeService pledge) => _pledge = pledge;

        // POST /api/PledgeDoc/{requestId}/printed
        // بترجّع { printedAt: "27/08/2026 09:41" } - نصّ جاهز يتطبع زي ما هو.
        [HttpPost("{requestId:int}/printed")]
        public async Task<IActionResult> Printed(int requestId)
        {
            var printedAt = await _pledge.RecordPrintAsync(requestId);

            // ⚠️ الطلب مالوش تعهّد ⇒ مفيش ورقة أصلًا. 404 لا 200 بتاريخ فاضي:
            //    الواجهة لازم تفرّق بين «الخادم ردّ» و«الخادم مالوش رد هنا».
            if (printedAt == null) return NotFound();

            return Ok(new { printedAt });
        }
    }
}
