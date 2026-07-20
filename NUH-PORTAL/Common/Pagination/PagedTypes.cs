namespace NUH_PORTAL.Common.Pagination
{
    // بارامترات الاستعلام (صفحة/حجم/بحث/ترتيب) — نسخة خفيفة self-contained
    public class QueryParams
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? FilterText { get; set; }
        public string? SortBy { get; set; }
        public bool SortAsc { get; set; } = false;
    }

    // نتيجة مقسّمة لصفحات
    public class QueryResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
