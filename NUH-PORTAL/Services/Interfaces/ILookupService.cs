using NUH_PORTAL.DTOs.Lookups;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Services.Interfaces
{
    public interface ILookupService
    {
        // قوائم منسدلة — النشِط فقط، الاسم مترجم حسب ثقافة الطلب
        Task<List<LookupItemDto>> GetCollegesAsync();
        Task<List<LookupItemDto>> GetDepartmentsAsync(int? collegeId);
        // ⚠️ BuildingItemDto لا LookupItemDto: النوع المشتقّ بيحمل أسلوب الترقيم
        //    والسعة، وبدونهم الشاشة بتملأ قوائم الشقق والغرف بأرقام مش موجودة
        //    في المبنى المختار. والنوع في التوقيع لا في القيمة عشان
        //    System.Text.Json بيتسلسل بالنوع المعلَن - الزيادة كانت هتضيع بصمت.
        Task<List<BuildingItemDto>> GetBuildingsAsync(Gender? gender);
        Task<List<LookupItemDto>> GetAcademicLevelsAsync();
        Task<List<TermItemDto>> GetTermsAsync();
    }
}
