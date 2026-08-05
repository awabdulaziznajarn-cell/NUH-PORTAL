using Microsoft.AspNetCore.Http;
using NUH_PORTAL.DTOs.Attachments;
using NUH_PORTAL.DTOs.StudentStatus;

namespace NUH_PORTAL.Services.Interfaces
{
    // منطق حالات مغادرة الطلاب (تخرج/فصل/تحويل/ترك سكن) + تعطيل حساب الشبكة
    public interface IStudentStatusService
    {
        // تسجيل إجراء حالة — بصلاحية students.changeStatus
        Task<StudentStatusResultDto> CreateStatusActionAsync(string studentNumber, string statusType, string notes, IFormFile? file);
        Task<StudentStatusStatsDto> GetStatsAsync();
        Task<List<RecentStatusActionDto>> GetRecentAsync();
        Task<List<StudentStatusActionDto>> GetStudentHistoryAsync(int studentId);
        Task<DownloadFileDto> GetActionAttachmentAsync(int actionId);
    }
}
