using NUH_PORTAL.DTOs.Lookups;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Services.Interfaces
{
    public interface ILookupService
    {
        // قوائم منسدلة — النشِط فقط، الاسم مترجم حسب ثقافة الطلب
        Task<List<LookupItemDto>> GetCollegesAsync();
        Task<List<LookupItemDto>> GetDepartmentsAsync(int? collegeId);
        Task<List<LookupItemDto>> GetBuildingsAsync(Gender? gender);
        Task<List<LookupItemDto>> GetAcademicLevelsAsync();
        Task<List<TermItemDto>> GetTermsAsync();
    }
}
