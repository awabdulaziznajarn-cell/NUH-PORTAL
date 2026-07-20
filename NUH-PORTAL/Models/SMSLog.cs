namespace NUH_PORTAL.Models
{
    public class SMSLog
    {
        public int Id { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SentDate { get; set; }
    }
}
