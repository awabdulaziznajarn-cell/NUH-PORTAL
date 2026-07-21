using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Requests;

namespace NUH_PORTAL.Services.Interfaces
{
    // منطق دورة حياة الطلبات (كل اللي كان جوه RequestsController)
    public interface IRequestService
    {
        Task<List<RequestDto>> GetAllAsync();
        Task<QueryResult<RequestDto>> GetPagedAsync(QueryParams queryParams, string? status, string? requestType);
        Task<RequestDetailsDto> GetDetailsAsync(int id);
        Task<List<RequestDto>> GetPendingAsync();
        Task<RequestDto> CreateAsync(RequestCreateDto dto);
        Task<RequestDto> ReviewAsync(int id, ReviewDto dto);
        Task<RequestDto> UpdateBulkIdAsync(int id, UpdateRequestDto dto);
    }
}
