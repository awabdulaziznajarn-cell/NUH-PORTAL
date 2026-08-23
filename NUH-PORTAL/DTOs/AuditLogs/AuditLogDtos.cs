namespace NUH_PORTAL.DTOs.AuditLogs
{
    // فلتر موحّد لكل استعلامات سجل العمليات
    public class AuditLogFilter
    {
        public int? UserId { get; set; }
        public string? ActionGroup { get; set; }
        public string? Action { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? Search { get; set; }
        public string? SortBy { get; set; }
        public bool SortAsc { get; set; } = false;

        // ⚠️ تصفية على وحدة سكن أعضاء هيئة التدريس بعينها - يفتحها زر «التقرير
        //    الكامل» من نافذة الوحدة. الترقيم على مرجعين: التصحيح يُقيَّد على
        //    فترة الإشغال والتسليم على الوحدة، فالتصفية تشمل الاثنين معًا وإلا
        //    سقط نصف السجل بلا أن يلاحظ أحد.
        public int? FacultyUnitId { get; set; }
    }

    public class AuditChangeDto
    {
        public string? FieldName { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public class AuditLogUserDto
    {
        public string? full_name { get; set; }
        public string? username { get; set; }
    }

    // نفس شكل عنصر السجل القديم بالحرف
    public class AuditLogItemDto
    {
        public int Id { get; set; }
        public int? user_id { get; set; }
        public string? action { get; set; }
        public string? target_table { get; set; }
        public int target_id { get; set; }
        // ⚠️ الاسم المقروء للهدف: «برج 6 - شقة 20» بدل «FacultyUnits / 53».
        //    كان التصدير لإكسل بيترجم الرقم لاسم والشاشة لأ - فالموظف بيقرا
        //    على الشاشة أرقامًا ويصدّر الملف فيلاقي أسماء. نفس المصدر للاتنين
        //    دلوقتي (AuditLogQueryService.ResolveTargetNamesAsync).
        public string? target_name { get; set; }
        // ⚠️ المعرّف التقني للهدف جنب اسمه: حساب الدومين لوحدة السكن مثلًا.
        //    الاسم العربي بيقول «أنهي وحدة»، والحساب هو اللي بيتكتب في الدليل
        //    وبيتبحث بيه فعلًا - فالاتنين مطلوبين، وكل واحد بشكله على الشاشة.
        public string? target_sub { get; set; }
        public DateTime action_at { get; set; }
        public string? ip_address { get; set; }
        public string? user_agent { get; set; }
        public string? user_name { get; set; }
        public AuditLogUserDto? user { get; set; }
        public List<AuditChangeDto>? changes { get; set; }
    }

    public class AuditLogsStatsDto
    {
        public int TotalRecords { get; set; }
        public int TotalUsers { get; set; }
        public int StudentOperations { get; set; }
        public int RequestOperations { get; set; }
    }

    public class AuditLogsPageDto
    {
        public List<AuditLogItemDto> Data { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public AuditLogsStatsDto Stats { get; set; } = new();
    }

    public class DateCountDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class ActionCountDto
    {
        public string? Action { get; set; }
        public int Count { get; set; }
    }

    public class ChartDataDto
    {
        public List<DateCountDto> Last7Days { get; set; } = new();
        public List<DateCountDto> Login30 { get; set; } = new();
        public List<ActionCountDto> StudentOps { get; set; } = new();
        public List<ActionCountDto> RequestOps { get; set; } = new();
    }

    public class AlertDto
    {
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public int Count { get; set; }
        public string? message_ar { get; set; }
        public string? message_en { get; set; }
    }

    public class TodayStatsDto
    {
        public int TodayLogins { get; set; }
        public int TodayStudentOps { get; set; }
        public int TodayRequestOps { get; set; }
        public int TodayFailedLogins { get; set; }
        public int TodayDeletes { get; set; }
        public int TodayActiveUsers { get; set; }
        public int TodayTotalOps { get; set; }
    }

    public class AuditUserOptionDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

}
