using NUH_PORTAL.DTOs.AuditLogs;
using NUH_PORTAL.DTOs.Common;

namespace NUH_PORTAL.Services.Interfaces
{
    // استعلامات وتقارير سجل العمليات (كل اللي كان جوه AuditLogsController)
    public interface IAuditLogQueryService
    {
        Task<AuditLogsPageDto> GetLogsAsync(int page, int pageSize, AuditLogFilter filter);
        Task<FileResultDto> ExportLogsAsync(AuditLogFilter filter);
        Task<ChartDataDto> GetChartDataAsync();
        Task<List<AlertDto>> GetAlertsAsync();
        Task<string> GetReportHtmlAsync(string? type, int? userId, string? fromDate, string? toDate, string lang);
        Task<TodayStatsDto> GetTodayStatsAsync();
        Task<List<AuditUserOptionDto>> GetUsersAsync();
    }
}
