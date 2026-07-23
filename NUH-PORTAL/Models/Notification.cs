using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int request_id { get; set; }
        public string? channel { get; set; }
        public string? recipient_role { get; set; }
        public string? message { get; set; }
        public NotificationStatus? status { get; set; }
        public DateTime? sent_at { get; set; }

        // علاقة الطلب (FK) — كانت ناقصة: request_id كان مجرد int من غير قيد مرجعي
        public Request? Request { get; set; }
    }
}