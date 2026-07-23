namespace NUH_PORTAL.Models
{
    // قائمة المستويات الأكاديمية (lookup مُدار)
    public class AcademicLevel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // 1..5
        public string ArName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
