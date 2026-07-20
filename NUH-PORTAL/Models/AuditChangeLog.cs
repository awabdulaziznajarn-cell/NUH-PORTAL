namespace NUH_PORTAL.Models
{
    public class AuditChangeLog
    {
        public int Id { get; set; }
        public int AuditLogId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public AuditLog? AuditLog { get; set; }
    }
}