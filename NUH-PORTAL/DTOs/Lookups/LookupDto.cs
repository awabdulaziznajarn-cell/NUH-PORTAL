using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Lookups
{
    // قراءة كاملة لعنصر lookup (لشاشة الإدارة) — يغطّي College/Building/AcademicLevel/Department
    public class LookupDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string ArName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }

        // خاص بالمبنى
        public Gender? Gender { get; set; }
        // خاص بالقسم
        public int? CollegeId { get; set; }
        public string? CollegeName { get; set; }
    }
}
