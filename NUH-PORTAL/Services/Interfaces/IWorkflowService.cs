using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // محرك سجل الـ workflow والتدقيق منخفض المستوى (بيسمح بتسجيل audit بدون مستخدم)
    public interface IWorkflowService
    {
        Task LogAuditAsync(int? userId, string action, string targetTable, int targetId, string? ipAddress = null, string? userAgent = null);
        Task<string?> GetPreviousStageAsync(int requestId);
        Task LogTransitionAsync(int requestId, string? fromStage, string toStage, int actionBy, string? notes = null);
        Task<List<WorkflowHistory>> GetHistoryAsync(int requestId);
        Task<string?> GetCurrentStageAsync(int requestId);
    }
}
