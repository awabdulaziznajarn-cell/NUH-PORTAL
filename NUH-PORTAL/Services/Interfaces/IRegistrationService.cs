using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // محرك التسجيل الذاتي: أرقام الطلبات، فحص التكرار، الإنشاء، والانتقالات بين المراحل
    public interface IRegistrationService
    {
        Task<string> GenerateRequestNumberAsync();
        Task<bool> CheckDuplicateByMobileAsync(string mobile, int? excludeRequestId = null);
        Task<bool> CheckDuplicateByStudentIdAsync(string studentId, int? excludeRequestId = null);
        Task<Request> CreateRegistrationRequestAsync(int studentId, string requestNumber, string registrationData, int submittedBy);
        Task<bool> ApproveAsSupervisorAsync(int requestId, int supervisorId, string? notes = null);
        Task<bool> RejectAsSupervisorAsync(int requestId, int supervisorId, string? notes = null);
        Task<bool> ApproveAsCyberAsync(int requestId, int cyberId, string? notes = null);
        Task<bool> RejectAsCyberAsync(int requestId, int cyberId, string? notes = null);
        Task<bool> ApproveAsAdminAsync(int requestId, int adminId, string? notes = null);
        Task<bool> RejectAsAdminAsync(int requestId, int adminId, string? notes = null);
        Task<bool> RequestMoreInfoAsync(int requestId, int reviewerId, string notes, string? fromStage = null);
        Task<string?> ResubmitRequestAsync(int requestId, int userId, string? registrationData = null);
    }
}
