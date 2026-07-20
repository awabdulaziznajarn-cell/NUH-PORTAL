namespace NUH_PORTAL.DTOs.Students
{
    // سجل دورة حياة حساب الطالب (نفس شكل الرد القديم)
    public class LifecycleLogDto
    {
        public int Id { get; set; }
        public string? Action { get; set; }
        public DateTime PerformedAt { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public string? PerformerName { get; set; }
    }
}
