using Microsoft.AspNetCore.Http;
using NUH_PORTAL.DTOs.Attachments;
using NUH_PORTAL.DTOs.Housing;

namespace NUH_PORTAL.Services.Interfaces
{
    // نقل سكن الطلاب بواسطة المشرف
    public interface ISupervisorHousingTransferService
    {
        Task<TransferResultDto> CreateTransferAsync(string studentNumber, string newBuilding, string newFloor, string newApartment, string newRoom, string reason, string? customReason, IFormFile? file);
        Task<List<RecentTransferDto>> GetRecentAsync();
        Task<DownloadFileDto> GetAttachmentAsync(int transferId);
    }
}
