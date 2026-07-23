namespace NUH_PORTAL.Models
{
    // بند تعهّد/شرط سكن يظهر للطالب وقت التسجيل (كيان مُدار) — زي Term في مشروع الـ permit
    public class Term
    {
        public int Id { get; set; }
        public string ArText { get; set; } = string.Empty;
        public string EnText { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
