namespace NUH_PORTAL.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public int? user_id { get; set; }
        public string? action { get; set; }
        public string? target_table { get; set; }
        public int target_id { get; set; }
        public DateTime action_at { get; set; }
        public string? ip_address { get; set; }
        public string? user_agent { get; set; }
        public User? User { get; set; }
        public List<AuditChangeLog>? AuditChangeLogs { get; set; }
    }
}