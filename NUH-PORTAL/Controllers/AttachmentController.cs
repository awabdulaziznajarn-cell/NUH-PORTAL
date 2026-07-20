using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AttachmentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly WorkflowService _workflowService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AttachmentController> _logger;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".xls", ".xlsx"
        };

        private const long MaxFileSize = 10 * 1024 * 1024;

        public AttachmentController(AppDbContext context, WorkflowService workflowService,
            IWebHostEnvironment env, ILogger<AttachmentController> logger)
        {
            _context = context;
            _workflowService = workflowService;
            _env = env;
            _logger = logger;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> Upload([FromForm] int requestId, [FromForm] string? documentType, [FromForm] string? notes)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.RequestType != "self_registration")
                return NotFound(new { message = "الطلب غير موجود" });

            var files = HttpContext.Request.Form.Files;
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "يرجى اختيار ملف للرفع" });

            var uploaded = new List<object>();

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedTypes.Contains(ext))
                    return BadRequest(new { message = $"نوع الملف {ext} غير مسموح به. الأنواع المسموحة: {string.Join(", ", AllowedTypes)}" });

                if (file.Length > MaxFileSize)
                    return BadRequest(new { message = "حجم الملف يتجاوز الحد المسموح به (10MB)" });

                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "requests", requestId.ToString());
                Directory.CreateDirectory(uploadDir);

                var storedName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadDir, storedName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var attachment = new RequestAttachment
                {
                    RequestId = requestId,
                    FileName = storedName,
                    OriginalFileName = file.FileName,
                    ContentType = file.ContentType ?? "application/octet-stream",
                    FileSize = file.Length,
                    DocumentType = documentType,
                    Notes = notes,
                    UploadedBy = actorId,
                    UploadedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _context.RequestAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var ua = HttpContext.Request.Headers.UserAgent.ToString();
                await _workflowService.LogAuditAsync(actorId, "attachment_uploaded", "RequestAttachments", attachment.Id, ip, ua);

                uploaded.Add(new
                {
                    attachment.Id,
                    attachment.OriginalFileName,
                    attachment.FileSize,
                    attachment.ContentType,
                    attachment.DocumentType,
                    attachment.UploadedAt
                });
            }

            return Ok(new { message = "تم رفع الملفات بنجاح", files = uploaded });
        }

        [HttpGet("{requestId}/list")]
        public async Task<IActionResult> List(int requestId)
        {
            var attachments = await _context.RequestAttachments
                .Where(a => a.RequestId == requestId && !a.IsDeleted)
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new
                {
                    a.Id,
                    a.OriginalFileName,
                    a.FileSize,
                    a.ContentType,
                    a.DocumentType,
                    a.Notes,
                    a.UploadedAt,
                    UploadedByName = a.UploadedByUser != null ? a.UploadedByUser.full_name ?? a.UploadedByUser.username : null
                })
                .ToListAsync();

            return Ok(attachments);
        }

        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(int id)
        {
            var attachment = await _context.RequestAttachments
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

            if (attachment == null)
                return NotFound(new { message = "الملف غير موجود" });

            var filePath = Path.Combine(_env.WebRootPath, "uploads", "requests",
                attachment.RequestId.ToString(), attachment.FileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = "الملف غير موجود على الخادم" });

            return PhysicalFile(filePath, attachment.ContentType, attachment.OriginalFileName);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId == 0) return Unauthorized();

            var attachment = await _context.RequestAttachments
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

            if (attachment == null)
                return NotFound(new { message = "الملف غير موجود" });

            attachment.IsDeleted = true;
            await _context.SaveChangesAsync();

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            await _workflowService.LogAuditAsync(actorId, "attachment_deleted", "RequestAttachments", id, ip, ua);

            return Ok(new { message = "تم حذف الملف" });
        }
    }
}
