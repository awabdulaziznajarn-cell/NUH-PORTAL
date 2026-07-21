using Microsoft.AspNetCore.Http;
using NUH_PORTAL.DTOs.Housing;

namespace NUH_PORTAL.Services.Interfaces
{
    // نقل سكن الطلاب بواسطة المشرف
    public interface ISupervisorHousingTransferService
    {
        Task<TransferResultDto> CreateTransferAsync(string studentNumber, string newBuilding, string newApartment, string newRoom, string reason, string? customReason, IFormFile? file);
        Task<List<RecentTransferDto>> GetRecentAsync();
    }
}
