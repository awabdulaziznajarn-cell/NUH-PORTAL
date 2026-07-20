namespace NUH_PORTAL.Models
{
    public class WorkflowHistory
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string? FromStage { get; set; }
        public string ToStage { get; set; } = string.Empty;
        public int ActionBy { get; set; }
        public DateTime ActionDate { get; set; }
        public string? Notes { get; set; }

        public Request? Request { get; set; }
        public User? Actor { get; set; }
    }
}
