namespace NUH_PORTAL.Models
{
    // سجل الأخطاء المرصودة (من الـ ExceptionHandlingMiddleware) — للتشخيص.
    public class ErrorLog
    {
        public int Id { get; set; }
        public DateTime occurred_at { get; set; }
        public string? message { get; set; }
        public string? exception_type { get; set; }
        public string? stack_trace { get; set; }
        public string? source { get; set; }
        public string? request_path { get; set; }
        public string? request_method { get; set; }
        public int status_code { get; set; }
        public int? user_id { get; set; }
        public string? username { get; set; }
        public string? ip_address { get; set; }
        public string? user_agent { get; set; }

        public User? User { get; set; }
    }
}
