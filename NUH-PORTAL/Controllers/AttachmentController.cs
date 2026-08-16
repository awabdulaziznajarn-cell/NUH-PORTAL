using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق المرفقات في IAttachmentService
    // مرفقات الطلبات (صور الهويات) = وثائق حسّاسة؛ لحاملي requests.attachments فقط، مش لتوكن الطالب.
    // ⚠️ كانت "requests.process" — واسم مش موجود في ApplicationPermissions.
    //    Program.cs بيسجّل policy لكل صلاحية في القائمة بس، فالاسم ده مالوش
    //    policy، والتفويض كان بيرمي استثناء والرد بيطلع 500. يعني كل مرفقات
    //    الطلبات (رفع/عرض/تنزيل/حذف) كانت واقفة. الصلاحية المصمّمة لده
    //    اسمها requests.attachments وكانت معرّفة ومش مستخدمة في أي سطر.
    [Authorize(Policy = "requests.attachments")]
    [Route("api/[controller]")]
    [ApiController]
    public class AttachmentController : ControllerBase
    {
        private readonly IAttachmentService _service;

        public AttachmentController(IAttachmentService service) => _service = service;

        // POST api/Attachment/upload (multipart)
        [HttpPost("upload")]
        [RequestSizeLimit(Services.AttachmentService.MaxFileSize)]
        public async Task<IActionResult> Upload([FromForm] int requestId, [FromForm] string? documentType, [FromForm] string? notes)
        {
            var uploaded = await _service.UploadAsync(requestId, documentType, notes, HttpContext.Request.Form.Files);
            return Ok(new { message = "تم رفع الملفات بنجاح", files = uploaded });
        }

        // GET api/Attachment/{requestId}/list
        [HttpGet("{requestId}/list")]
        public async Task<IActionResult> List(int requestId)
            => Ok(await _service.ListAsync(requestId));

        // GET api/Attachment/download/{id}
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(int id)
        {
            var file = await _service.GetDownloadAsync(id);
            return PhysicalFile(file.FilePath, file.ContentType, file.OriginalFileName);
        }

        // DELETE api/Attachment/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return Ok(new { message = "تم حذف الملف" });
        }
    }
}
