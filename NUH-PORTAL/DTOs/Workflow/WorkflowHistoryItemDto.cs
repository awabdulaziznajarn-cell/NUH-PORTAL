namespace NUH_PORTAL.DTOs.Workflow
{
    public class WorkflowHistoryItemDto
    {
        public string? FromStage { get; set; }
        public string? ToStage { get; set; }
        public DateTime ActionDate { get; set; }
        public string? Notes { get; set; }
        public string? ActorName { get; set; }
    }
}
