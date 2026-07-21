using Microsoft.AspNetCore.Http;
using NUH_PORTAL.DTOs.Bulk;
using NUH_PORTAL.DTOs.Common;

namespace NUH_PORTAL.Services.Interfaces
{
    // التسجيل الجماعي من ملفات Excel (كل اللي كان جوه BulkRegistrationController)
    public interface IBulkRegistrationService
    {
        FileResultDto GetTemplate();
        Task<BulkValidationResultDto> ValidateFileAsync(IFormFile? file);
        Task<BulkCreateResultDto> CreateAsync(BulkCreateDto dto);
        Task<List<BulkRequestDto>> GetAllAsync();
        Task<BulkRequestDetailsDto> GetByIdAsync(int id);
    }
}
