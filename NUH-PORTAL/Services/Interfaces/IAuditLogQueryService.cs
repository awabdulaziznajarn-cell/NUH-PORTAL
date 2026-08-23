using NUH_PORTAL.DTOs.AuditLogs;
using NUH_PORTAL.DTOs.Common;

namespace NUH_PORTAL.Services.Interfaces
{
    // استعلامات وتقارير سجل العمليات (كل اللي كان جوه AuditLogsController)
    public interface IAuditLogQueryService
    {
        Task<AuditLogsPageDto> GetLogsAsync(int page, int pageSize, AuditLogFilter filter);
        Task<FileResultDto> ExportLogsAsync(AuditLogFilter filter, string? calendar = null);
        Task<ChartDataDto> GetChartDataAsync(string? fromDate = null, string? toDate = null);
        Task<List<AlertDto>> GetAlertsAsync();
        Task<string> GetReportHtmlAsync(string? type, int? userId, string? fromDate, string? toDate, string lang, string? calendar = null);
        Task<TodayStatsDto> GetTodayStatsAsync(string? fromDate = null, string? toDate = null);
        Task<List<AuditUserOptionDto>> GetUsersAsync();
    }
}
