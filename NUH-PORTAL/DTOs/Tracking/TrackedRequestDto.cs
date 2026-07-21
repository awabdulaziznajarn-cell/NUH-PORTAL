namespace NUH_PORTAL.DTOs.Tracking
{
    // عنصر في نتيجة التتبع برقم الجوال (نفس شكل الرد القديم)
    public class TrackedRequestDto
    {
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? StudentName { get; set; }
    }
}
