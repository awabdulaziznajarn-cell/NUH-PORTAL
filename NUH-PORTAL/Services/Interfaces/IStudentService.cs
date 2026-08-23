using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Students;

namespace NUH_PORTAL.Services.Interfaces
{
    // منطق الطلاب (كل اللي كان جوه StudentsController)
    public interface IStudentService
    {
        Task<List<StudentDto>> GetStudentsAsync(bool showDeleted, string? adStatus);
        Task<QueryResult<StudentDto>> GetPagedAsync(QueryParams queryParams, bool showDeleted, string? adStatus);
        Task<StudentStatsDto> GetStatsAsync();
        // عدد الساكنين في كل مبنى — مفلتر بجنس المستخدم مثل باقي قوائم الطلاب
        Task<List<BuildingCountDto>> GetCountByBuildingAsync();
        Task<StudentDto> GetByIdAsync(int id);
        // بحث بالرقم الجامعي — بيرجّع null لو مش موجود بدل ما يرمي استثناء،
        // عشان شاشات الإدخال تعرض «الطالب غير موجود» من غير ضجيج في سجل الأخطاء.
        Task<StudentDto?> GetByStudentNumberAsync(string studentNumber);
        Task<StudentDto> CreateAsync(StudentCreateDto dto);
        Task<StudentDto> UpdateAsync(int id, StudentUpdateDto dto);
        Task DeleteAsync(int id);
        Task RestoreAsync(int id);
        Task<List<LifecycleLogDto>> GetLifecycleAsync(int id);
    }
}
