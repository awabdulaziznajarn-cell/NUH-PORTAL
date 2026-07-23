using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Requests
{
    // مدخلات إنشاء طلب — الحقول اللي المستخدم يتحكم فيها فقط.
    // (الحالة/التواريخ/هوية المُقدِّم بيحددها السيرفر — ده بيقفل ثغرة over-posting اللي كانت موجودة)
    public class RequestCreateDto
    {
        public RequestType? RequestType { get; set; }
        public int StudentId { get; set; }
        public string? Notes { get; set; }
        public string? RequestNumber { get; set; }
        public int? BulkRequestId { get; set; }
        public string? RegistrationData { get; set; }
    }
}
