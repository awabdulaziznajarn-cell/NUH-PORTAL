using NUH_PORTAL.DTOs.Tracking;

namespace NUH_PORTAL.Services.Interfaces
{
    // تتبع الطلبات للجمهور (بدون تسجيل دخول)
    public interface IRequestTrackingService
    {
        Task<List<TrackedRequestDto>> TrackByMobileAsync(string mobile);
        // last4 = آخر ٤ أرقام من جوال الطالب. مطلوبة عشان رقم الطلب لوحده متسلسل
        // (2026-000001, 000002, ...) فكان ممكن حد يعدّي عليه بالترتيب. المعلومتين
        // مع بعض بتخلّي التخمين غير عملي، وبتكلفة صفر (من غير رسائل ولا تسجيل دخول).
        Task<TrackingDetailsDto> TrackByNumberAsync(string requestNumber, string? last4);
    }
}
