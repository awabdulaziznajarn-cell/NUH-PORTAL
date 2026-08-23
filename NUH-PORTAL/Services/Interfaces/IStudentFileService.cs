using NUH_PORTAL.DTOs.Students;

namespace NUH_PORTAL.Services.Interfaces
{
    // ملف الطالب المجمّع (شاشة الأمن السيبراني) - البحث برقم جامعي أو رقم هوية.
    public interface IStudentFileService
    {
        Task<StudentFileDto> GetAsync(string query, int? requestId);
    }
}
