namespace NUH_PORTAL.Models
{
    // قائمة الأقسام (lookup مُدار) — ممكن ترتبط بكلية (اختياري) لدعم الهرمية
    public class Department
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // cs, computer, electrical...
        public string ArName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        // ربط اختياري بالكلية (هرمية قسم ← كلية)
        public int? CollegeId { get; set; }
        public College? College { get; set; }
    }
}
