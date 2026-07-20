namespace NUH_PORTAL.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int request_id { get; set; }
        public string? channel { get; set; }
        public string? recipient_role { get; set; }
        public string? message { get; set; }
        public string? status { get; set; }
        public DateTime? sent_at { get; set; }
    }
}