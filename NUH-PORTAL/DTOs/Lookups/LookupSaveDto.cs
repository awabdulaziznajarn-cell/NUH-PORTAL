using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Lookups
{
    // إنشاء/تعديل عنصر lookup. الحقول الزيادة (Gender/CollegeId) تُستخدم حسب النوع فقط.
    public class LookupSaveDto
    {
        public string Code { get; set; } = string.Empty;
        public string ArName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public Gender? Gender { get; set; }
        public int? CollegeId { get; set; }
    }
}
