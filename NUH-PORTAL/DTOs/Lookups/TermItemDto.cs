namespace NUH_PORTAL.DTOs.Lookups
{
    // بند تعهّد للعرض — النص مترجم حسب ثقافة الطلب
    public class TermItemDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}
