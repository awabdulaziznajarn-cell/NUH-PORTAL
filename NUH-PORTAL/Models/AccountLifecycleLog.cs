namespace NUH_PORTAL.Models
{
    public class AccountLifecycleLog
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string? Action { get; set; }
        public int PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }

        public Student? Student { get; set; }
        public User? Performer { get; set; }
    }
}
