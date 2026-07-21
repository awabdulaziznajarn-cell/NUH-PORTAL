using Microsoft.AspNetCore.Http;
using NUH_PORTAL.DTOs.Attachments;

namespace NUH_PORTAL.Services.Interfaces
{
    // مرفقات طلبات التسجيل الذاتي
    public interface IAttachmentService
    {
        Task<List<UploadedFileDto>> UploadAsync(int requestId, string? documentType, string? notes, IFormFileCollection files);
        Task<List<AttachmentListItemDto>> ListAsync(int requestId);
        Task<DownloadFileDto> GetDownloadAsync(int id);
        Task DeleteAsync(int id);
    }
}
