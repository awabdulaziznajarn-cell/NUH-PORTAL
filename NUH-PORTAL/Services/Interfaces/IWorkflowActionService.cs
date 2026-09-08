using NUH_PORTAL.DTOs.Workflow;

namespace NUH_PORTAL.Services.Interfaces
{
    // طوابير المراجعة + اعتماد/رفض/طلب معلومات + سجل الطلب (كل اللي كان جوه WorkflowController)
    public interface IWorkflowActionService
    {
        Task<List<QueueItemDto>> GetQueueAsync(string? stage);
        Task<List<StatusCountDto>> GetQueueCountsAsync();
        // عدد الطلبات المستنية إجراء من المستخدم الحالي — للشارة في القائمة الجانبية
        Task<int> GetMyQueueCountAsync();
        // ⚠️ التسكين جزء من الاعتماد لا خطوة بعده: مرحلة إدارة الإسكان هي
        //    اللحظة اللي بيتقرّر فيها الطالب هيسكن فين، والاعتماد بلا تسكين
        //    كان بيخلّي طالبًا معتمَدًا بلا غرفة ومحدش فاكر يرجعله.
        //    الخانات فاضية في المراحل التانية.
        Task ApproveAsync(int requestId, string? notes,
                          string? housingBuilding = null, string? floorNumber = null,
                          string? apartmentNumber = null, string? roomNumber = null);
        Task RejectAsync(int requestId, string? notes);
        Task RequestMoreInfoAsync(int requestId, string? notes, List<string>? fields = null);
        Task<List<WorkflowHistoryItemDto>> GetHistoryAsync(int requestId);
    }
}
