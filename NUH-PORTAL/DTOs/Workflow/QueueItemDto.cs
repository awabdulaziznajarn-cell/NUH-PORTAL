namespace NUH_PORTAL.DTOs.Workflow
{
    // عنصر في طابور المراجعة (نفس شكل الرد القديم)
    public class QueueItemDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? StudentName { get; set; }
        public string? StudentId { get; set; }   // الرقم الجامعي (نص) زي الرد القديم
        public string? Notes { get; set; }
    }
}
