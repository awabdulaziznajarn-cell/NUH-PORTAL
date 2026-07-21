using NUH_PORTAL.DTOs.Tracking;

namespace NUH_PORTAL.Services.Interfaces
{
    // تتبع الطلبات للجمهور (بدون تسجيل دخول)
    public interface IRequestTrackingService
    {
        Task<List<TrackedRequestDto>> TrackByMobileAsync(string mobile);
        Task<TrackingDetailsDto> TrackByNumberAsync(string requestNumber);
    }
}
