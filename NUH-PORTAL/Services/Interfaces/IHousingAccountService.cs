using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Housing;
using NUH_PORTAL.DTOs.Students;

namespace NUH_PORTAL.Services.Interfaces
{
    // إدارة حسابات AD لطلاب السكن (كل اللي كان جوه HousingAccountManagementController)
    public interface IHousingAccountService
    {
        Task<List<HousingAccountListItemDto>> GetAllAsync(string? status);
        Task<QueryResult<HousingAccountListItemDto>> GetPagedAsync(QueryParams queryParams, string? status);
        Task<HousingAccountDetailsDto> GetDetailsAsync(int studentId);
        Task<ToggleAccountResultDto> EnableAccountAsync(int studentId);
        Task<ToggleAccountResultDto> DisableAccountAsync(int studentId);
        Task ResetPasswordAsync(int studentId, ResetPasswordDto dto);
        Task<ReProvisionResultDto> ReProvisionAsync(int studentId);
        Task SyncExtensionAttributesAsync(int studentId);
        Task<AdSearchResultDto> SearchADUsersAsync(string? q, int max);
        Task<List<LifecycleLogDto>> GetLifecycleLogsAsync(int studentId, int limit);
        Task<List<AdConfigurationDto>> GetAdConfigAsync();
        Task UpdateAdConfigAsync(List<AdConfigDto> configs);
        Task<HousingStatsDto> GetHousingStatsAsync();
        // مزامنة مع الأكتف دايركتوري — قراءة من الدومين وكتابة عندنا بس.
        // LinkNew = ربط طلاب مالهمش حساب مسجّل، RefreshLinked = تحديث حالة المربوطين.
        Task<AdLinkResultDto> SyncAdAccountsAsync(AdSyncMode mode, bool dryRun);
    }
}
