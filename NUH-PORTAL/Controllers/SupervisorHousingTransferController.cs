using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Core;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق النقل في ISupervisorHousingTransferService
    // بقت بصلاحية housing.transfer بدل الدور — أي دور تديله الصلاحية يقدر ينقل،
    // وسجل التنقلات بيسجّل مين نفّذ فالمساءلة محفوظة.
    [Authorize(Policy = "housing.transfer")]
    [Route("api/supervisor/housing-transfer")]
    [ApiController]
    public class SupervisorHousingTransferController : ControllerBase
    {
        private readonly ISupervisorHousingTransferService _service;

        public SupervisorHousingTransferController(ISupervisorHousingTransferService service) => _service = service;

        // POST api/supervisor/housing-transfer (multipart)
        [HttpPost]
        [RequestSizeLimit(Services.SupervisorHousingTransferService.MaxFileSize)]
        public async Task<IActionResult> CreateTransfer(
            [FromForm] string studentNumber,
            [FromForm] string newBuilding,
            [FromForm] string newFloor,
            [FromForm] string newApartment,
            [FromForm] string newRoom,
            [FromForm] string reason,
            [FromForm] string? customReason,
            IFormFile? file)
        {
            var result = await _service.CreateTransferAsync(studentNumber, newBuilding, newFloor, newApartment, newRoom, reason, customReason, file);
            return Ok(new { message = result.Message, transferId = result.TransferId, oldLocation = result.OldLocation, newLocation = result.NewLocation });
        }

        // GET api/supervisor/housing-transfer/recent
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent([FromQuery] int skip = 0)
            => Ok(await _service.GetRecentAsync(skip));

        // GET api/supervisor/housing-transfer/{id}/attachment[?download=true]
        // من غير download بيرجع inline — يعني الصور و PDF بتتعرض في المتصفح
        // بدل ما تتنزّل على طول.
        // ⚠️ ممنوع تخزينه في أي كاش. الرد ده نتيجة *قرار صلاحية* يخصّ
        //    المستخدم الحالي، والمتصفح بيتعامل معاه كملف عادي فبيخزّنه
        //    بالرابط — فنفس الرابط بيتفتح تاني من الكاش من غير ما يوصل
        //    للسيرفر أصلًا، فالفحص ما بيتنفّذش. ده مش سيناريو نظري:
        //    كان بيخلّي اختبار «مشرف القسم التاني» يبان ناجح وهو مش ناجح.
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [HttpGet("{id}/attachment")]
        public async Task<IActionResult> GetAttachment(int id, [FromQuery] bool download = false)
        {
            var file = await _service.GetAttachmentAsync(id);

            // ⚠️ الطبقة الرابعة - سياسة الردّ نفسها. مصدرها الوحيد
            //    Core/AttachmentPolicy، وتُنادى قبل بناء الردّ لأن الترويسات
            //    تُكتب مع بدء إرسال الجسم فلا تُقبل بعده.
            AttachmentPolicy.ApplyResponseHeaders(Response);

            // ⚠️ التنزيل إجباري لأي نوع خارج قائمة العرض الآمن في
            //    Core/AttachmentPolicy. المعاينة داخل الصفحة تعني تنفيذ المحتوى
            //    في أصل الموقع، فما لا يُعرض بأمان يخرج بترويسة تنزيل مهما طلب
            //    المستخدم. تمرير اسم الملف هو ما يجعلها attachment.
            return (download || !AttachmentPolicy.CanRenderInline(file.ContentType))
                ? PhysicalFile(file.FilePath, file.ContentType, file.OriginalFileName)
                : PhysicalFile(file.FilePath, file.ContentType);
        }
    }
}
