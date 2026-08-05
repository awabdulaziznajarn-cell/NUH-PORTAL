using NUH_PORTAL.DTOs.Registration;

namespace NUH_PORTAL.Services.Interfaces
{
    // تدفق التسجيل الذاتي للطلاب (كل اللي كان جوه RegistrationController)
    public interface IRegistrationFlowService
    {
        Task<StartRegistrationResultDto> StartAsync(StartRegistrationRequest request);
        Task AcceptDeclarationsAsync(int requestId, AcceptDeclarationsRequest request);
        Task<List<MyRequestListItemDto>> GetMyRequestsAsync(string? mobile);
        Task<MyRequestDetailDto> GetMyRequestDetailAsync(int requestId);
        Task ResubmitAsync(int requestId, ResubmitRequest request);
        Task<DuplicateCheckResultDto> CheckDuplicateAsync(DuplicateCheckRequest request);
    }
}
