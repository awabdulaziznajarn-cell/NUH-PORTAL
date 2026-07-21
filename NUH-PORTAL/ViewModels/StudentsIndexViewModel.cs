using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Students;

namespace NUH_PORTAL.ViewModels
{
    // موديل صفحة قائمة الطلاب (MVC) — أول صفحة بيانات + الإحصائيات جاهزين من السيرفر
    public class StudentsIndexViewModel
    {
        public StudentStatsDto Stats { get; set; } = new();
        public QueryResult<StudentDto> Page { get; set; } = new();
    }
}
