namespace NUH_PORTAL.DTOs.Lookups
{
    // عنصر قائمة منسدلة خفيف — الاسم مترجم حسب ثقافة الطلب الحالية
    public class LookupItemDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
