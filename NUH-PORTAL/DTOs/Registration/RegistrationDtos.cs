namespace NUH_PORTAL.DTOs.Registration
{
    public class StartRegistrationRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public object? RegistrationData { get; set; }
    }

    public class AcceptDeclarationsRequest
    {
        public bool DeclarationAccepted { get; set; }
        public bool PolicyAccepted { get; set; }
        public string? PolicyVersion { get; set; }
    }

    // فحص مبكر للتكرار من داخل النموذج.
    // ⚠️ الحقلان مطلوبان معًا عن قصد: فحص رقم الهوية وحده يحوّل المسار إلى أداة
    //    استعلام — يكتب المهاجم أرقام هوية بالتتابع فيعرف مَن المسجَّل في الإسكان.
    //    باشتراط تطابق الرقم الجامعي ورقم الهوية لنفس السجل، مَن يملك الاثنين
    //    صحيحين يعرف صاحبهما أصلًا فلا يكتسب معلومة جديدة.
    //    (نفس قاعدة شاشة التتبع: رقم الطلب وحده لا يكفي، ومعه آخر ٤ أرقام.)
    public class DuplicateCheckRequest
    {
        public string? StudentId { get; set; }
        public string? NationalId { get; set; }
    }

    public class DuplicateCheckResultDto
    {
        public bool Found { get; set; }
        public string? RequestNumber { get; set; }
        public string? Message { get; set; }
    }

    public class ResubmitRequest
    {
        public string? RegistrationData { get; set; }
    }

    public class StartRegistrationResultDto
    {
        public string? Message { get; set; }
        public int RequestId { get; set; }
        public string? RequestNumber { get; set; }
    }

    public class MyRequestListItemDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? StudentName { get; set; }
        public string? StudentId { get; set; }
        public string? StudentPhone { get; set; }
    }

    public class MyRequestStudentDto
    {
        public string? student_id { get; set; }
        public string? full_name { get; set; }
        public string? national_id { get; set; }
        public string? college { get; set; }
        public string? department { get; set; }
        public string? phone { get; set; }
        public string? ad_username { get; set; }
    }

    public class MyRequestDetailDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public string? RegistrationData { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? Notes { get; set; }
        public MyRequestStudentDto? Student { get; set; }
        public List<NUH_PORTAL.DTOs.Workflow.WorkflowHistoryItemDto> History { get; set; } = new();
    }
}
