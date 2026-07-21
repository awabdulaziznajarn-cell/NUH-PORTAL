using NUH_PORTAL.DTOs.ADSetup;

namespace NUH_PORTAL.Services.Interfaces
{
    // فحوصات جاهزية AD + محاكاة إنشاء حساب + اختبار البايبلاين كامل (أدوات تشخيص للأدمن)
    public interface IADSetupService
    {
        Task<ADReadinessReport> GetReadinessAsync();
        Task<ADDryRunResult> GetDryRunAsync(string studentId);
        Task<ADTestUserReport> RunTestUserAsync(ADTestUserRequest request);
    }
}
