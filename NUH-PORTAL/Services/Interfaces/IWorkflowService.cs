using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // محرك سجل الـ workflow والتدقيق منخفض المستوى (بيسمح بتسجيل audit بدون مستخدم)
    public interface IWorkflowService
    {
        Task LogAuditAsync(int? userId, string action, string targetTable, int targetId, string? ipAddress = null, string? userAgent = null);
        Task<string?> GetPreviousStageAsync(int requestId);
        Task LogTransitionAsync(int requestId, string? fromStage, string toStage, int actionBy, string? notes = null, string? changesJson = null);
        Task<List<WorkflowHistory>> GetHistoryAsync(int requestId);
        // آخر خطوة سجّل فيها الطالب تعديلات (changes_json) — لتعليم الحقول في شاشة التفاصيل
        Task<(string? Json, DateTime? At)> GetLastChangesAsync(int requestId);
    }
}
