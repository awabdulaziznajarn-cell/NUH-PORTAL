namespace NUH_PORTAL.DTOs.StudentStatus
{
    // نتيجة تسجيل حالة مغادرة الطالب (نفس شكل الرد القديم)
    public class StudentStatusResultDto
    {
        public string? Message { get; set; }
        public string? Warning { get; set; }
        public int ActionId { get; set; }
        public string? StatusType { get; set; }
        public bool AdDisabled { get; set; }
        public string? AdError { get; set; }
    }
}
