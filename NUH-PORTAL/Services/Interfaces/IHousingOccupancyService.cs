using NUH_PORTAL.DTOs.Housing;

namespace NUH_PORTAL.Services.Interfaces
{
    // خريطة إشغال المباني - قراءة فقط، مافيش أي كتابة في الخدمة دي.
    public interface IHousingOccupancyService
    {
        Task<List<BuildingOccupancySummaryDto>> GetBuildingsAsync();
        Task<BuildingOccupancyDto> GetBuildingAsync(int buildingId);
    }
}
