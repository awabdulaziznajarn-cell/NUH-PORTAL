using NUH_PORTAL.DTOs.Logs;

namespace NUH_PORTAL.Services.Interfaces
{
    // استعلامات سجلّي الأخطاء والدخول/الخروج (قراءة + تقسيم صفحات + فلاتر).
    public interface ILogQueryService
    {
        Task<PagedResult<ErrorLogItemDto>> GetErrorLogsAsync(int page, int pageSize, string? search, string? fromDate, string? toDate, string? sortBy = null, bool sortAsc = false);
        Task<ErrorLogDetailDto> GetErrorLogAsync(int id);

        Task<PagedResult<SignInLogItemDto>> GetSignInLogsAsync(int page, int pageSize, string? eventType, string? search, string? fromDate, string? toDate, string? sortBy = null, bool sortAsc = false);
    }
}
