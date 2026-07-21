using Microsoft.AspNetCore.Http;
using NUH_PORTAL.DTOs.StudentStatus;

namespace NUH_PORTAL.Services.Interfaces
{
    // منطق حالات مغادرة الطلاب (تخرج/فصل/تحويل/ترك سكن) + تعطيل حساب الشبكة
    public interface IStudentStatusService
    {
        // مسار الأدمن (كل الأدوار عدا user و cyber)
        Task<StudentStatusResultDto> CreateStatusActionAsync(string studentNumber, string statusType, string notes, IFormFile? file);
        Task<StudentStatusStatsDto> GetStatsAsync();
        Task<List<RecentStatusActionDto>> GetRecentAsync();
        Task<List<StudentStatusActionDto>> GetStudentHistoryAsync(int studentId);

        // مسار المشرف (نفس المنطق بصياغة سجل مختلفة + سجلاته هو بس)
        Task<StudentStatusResultDto> CreateSupervisorStatusActionAsync(string studentNumber, string statusType, string notes, IFormFile? file);
        Task<List<SupervisorRecentActionDto>> GetRecentForSupervisorAsync();
    }
}
