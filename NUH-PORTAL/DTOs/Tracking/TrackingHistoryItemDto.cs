namespace NUH_PORTAL.DTOs.Tracking
{
    public class TrackingHistoryItemDto
    {
        public string? ToStage { get; set; }
        public DateTime ActionDate { get; set; }
        public string? Notes { get; set; }
        public string? ActorName { get; set; }
    }
}
