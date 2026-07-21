using NUH_PORTAL.DTOs.Otp;

namespace NUH_PORTAL.Services.Interfaces
{
    // إرسال والتحقق من OTP + إنشاء مستخدم الطالب وتوليد التوكن
    public interface IOtpFlowService
    {
        Task<SendOtpResultDto> SendAsync(SendOtpRequest request);
        Task<VerifyOtpResultDto> VerifyAsync(VerifyOtpRequest request);
    }
}
