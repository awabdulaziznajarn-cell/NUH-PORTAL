using MapsterMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Attachments;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // منطق مرفقات الطلبات — اتنقل من AttachmentController
    public class AttachmentService : AppServiceBase, IAttachmentService
    {
        private readonly IRepository<RequestAttachment> _attachments;
        private readonly IRepository<Request> _requests;
        private readonly IAuditService _audit;
        private readonly IWebHostEnvironment _env;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".xls", ".xlsx"
        };

        public const long MaxFileSize = 10 * 1024 * 1024;

        public AttachmentService(
            IRepository<RequestAttachment> attachments,
            IRepository<Request> requests,
            IAuditService audit,
            IWebHostEnvironment env,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _attachments = attachments;
            _requests = requests;
            _audit = audit;
            _env = env;
        }

        public async Task<List<UploadedFileDto>> UploadAsync(int requestId, string? documentType, string? notes, IFormFileCollection files)
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            var request = await _requests.GetByIdAsync(requestId);
            if (request == null || request.RequestType != "self_registration")
                throw UserFriendlyException.NotFound("الطلب غير موجود");

            if (files == null || files.Count == 0)
                throw new UserFriendlyException("يرجى اختيار ملف للرفع", 400);

            var uploaded = new List<UploadedFileDto>();

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedTypes.Contains(ext))
                    throw new UserFriendlyException($"نوع الملف {ext} غير مسموح به. الأنواع المسموحة: {string.Join(", ", AllowedTypes)}", 400);

                if (file.Length > MaxFileSize)
                    throw new UserFriendlyException("حجم الملف يتجاوز الحد المسموح به (10MB)", 400);

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

                await _attachments.AddAsync(attachment);
                await UnitOfWork.SaveAsync();

                await _audit.LogAsync("attachment_uploaded", "RequestAttachments", attachment.Id);

                uploaded.Add(new UploadedFileDto
                {
                    Id = attachment.Id,
                    OriginalFileName = attachment.OriginalFileName,
                    FileSize = attachment.FileSize,
                    ContentType = attachment.ContentType,
                    DocumentType = attachment.DocumentType,
                    UploadedAt = attachment.UploadedAt
                });
            }

            return uploaded;
        }

        public async Task<List<AttachmentListItemDto>> ListAsync(int requestId)
        {
            return await _attachments.Query().AsNoTracking()
                .Where(a => a.RequestId == requestId && !a.IsDeleted)
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new AttachmentListItemDto
                {
                    Id = a.Id,
                    OriginalFileName = a.OriginalFileName,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    DocumentType = a.DocumentType,
                    Notes = a.Notes,
                    UploadedAt = a.UploadedAt,
                    UploadedByName = a.UploadedByUser != null ? a.UploadedByUser.full_name ?? a.UploadedByUser.username : null
                })
                .ToListAsync();
        }

        public async Task<DownloadFileDto> GetDownloadAsync(int id)
        {
            var attachment = await _attachments.FindAsync(a => a.Id == id && !a.IsDeleted)
                ?? throw UserFriendlyException.NotFound("الملف غير موجود");

            var filePath = Path.Combine(_env.WebRootPath, "uploads", "requests",
                attachment.RequestId.ToString(), attachment.FileName);

            if (!File.Exists(filePath))
                throw UserFriendlyException.NotFound("الملف غير موجود على الخادم");

            return new DownloadFileDto
            {
                FilePath = filePath,
                ContentType = attachment.ContentType,
                OriginalFileName = attachment.OriginalFileName
            };
        }

        public async Task DeleteAsync(int id)
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            var attachment = await _attachments.FindAsync(a => a.Id == id && !a.IsDeleted)
                ?? throw UserFriendlyException.NotFound("الملف غير موجود");

            attachment.IsDeleted = true;
            await UnitOfWork.SaveAsync();

            await _audit.LogAsync("attachment_deleted", "RequestAttachments", id);
        }
    }
}
