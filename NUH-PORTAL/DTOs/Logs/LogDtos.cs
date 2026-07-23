namespace NUH_PORTAL.DTOs.Logs
{
    // نتيجة مقسّمة لصفحات (تتسلسل camelCase: data/page/pageSize/totalRecords/totalPages).
    public class PagedResult<T>
    {
        public List<T> Data { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
    }

    // صف في جدول سجل الأخطاء.
    public class ErrorLogItemDto
    {
        public int Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public string? Message { get; set; }
        public string? ExceptionType { get; set; }
        public int StatusCode { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestMethod { get; set; }
        public string? Username { get; set; }
        public string? IpAddress { get; set; }
    }

    // تفاصيل خطأ (المودال) — بيضيف الـ stack trace والمصدر.
    public class ErrorLogDetailDto : ErrorLogItemDto
    {
        public string? StackTrace { get; set; }
        public string? Source { get; set; }
        public string? UserAgent { get; set; }
        public int? UserId { get; set; }
    }

    // صف في جدول سجل الدخول والخروج.
    public class SignInLogItemDto
    {
        public int Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string EventType { get; set; } = "";
        public string? Method { get; set; }
        public bool Success { get; set; }
        public string? Detail { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}
