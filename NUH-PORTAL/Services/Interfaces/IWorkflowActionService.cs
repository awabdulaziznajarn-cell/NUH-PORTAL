using NUH_PORTAL.DTOs.Workflow;

namespace NUH_PORTAL.Services.Interfaces
{
    // طوابير المراجعة + اعتماد/رفض/طلب معلومات + سجل الطلب (كل اللي كان جوه WorkflowController)
    public interface IWorkflowActionService
    {
        Task<List<QueueItemDto>> GetQueueAsync(string? stage);
        Task<List<StatusCountDto>> GetQueueCountsAsync();
        Task ApproveAsync(int requestId, string? notes);
        Task RejectAsync(int requestId, string? notes);
        Task RequestMoreInfoAsync(int requestId, string? notes);
        Task<List<WorkflowHistoryItemDto>> GetHistoryAsync(int requestId);
    }
}
