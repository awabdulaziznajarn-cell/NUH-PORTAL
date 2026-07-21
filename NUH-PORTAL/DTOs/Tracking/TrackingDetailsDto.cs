namespace NUH_PORTAL.DTOs.Tracking
{
    // تفاصيل التتبع برقم الطلب (نفس شكل الرد القديم)
    public class TrackingDetailsDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? StudentName { get; set; }
        public string? AdUsername { get; set; }
        public List<TrackingHistoryItemDto> History { get; set; } = new();
    }
}
