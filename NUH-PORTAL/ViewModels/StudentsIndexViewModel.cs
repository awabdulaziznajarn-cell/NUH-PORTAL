using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Students;

namespace NUH_PORTAL.ViewModels
{
    // موديل صفحة قائمة الطلاب (MVC) — أول صفحة بيانات + الإحصائيات جاهزين من السيرفر
    public class StudentsIndexViewModel
    {
        public StudentStatsDto Stats { get; set; } = new();
        public QueryResult<StudentDto> Page { get; set; } = new();

        // ⚠️ نطاق المستخدم بيوصل للشاشة عشان العناوين تتغيّر معاه
        //    (Core/GenderScope.TitleKey). مابيتقراش في الـ View من الكليمات
        //    مباشرةً لأن القاعدة فيها استثناء صلاحية «الطلاب والطالبات معًا»،
        //    وده محسوب في UnitOfWork.GetGenderScope وحده.
        public NUH_PORTAL.Models.Enums.Gender? Scope { get; set; }
    }
}
