namespace NUH_PORTAL.Models
{
    // قائمة الكليات (lookup مُدار) — الاسم عربي/إنجليزي + كود ثابت + ترتيب + مفعّل
    public class College
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // مفتاح ثابت: engineering, medicine...
        public string ArName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Department> Departments { get; set; } = new List<Department>();
    }
}
