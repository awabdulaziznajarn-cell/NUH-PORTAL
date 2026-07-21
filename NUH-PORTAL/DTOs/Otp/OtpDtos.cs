namespace NUH_PORTAL.DTOs.Otp
{
    public class SendOtpRequest
    {
        public string Mobile { get; set; } = string.Empty;
    }

    public class VerifyOtpRequest
    {
        public string Mobile { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class SendOtpResultDto
    {
        public string? Message { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? Mode { get; set; }
    }

    // نفس أسماء الحقول القديمة بالحرف (snake_case) للحفاظ على الـ JSON
    public class OtpUserDto
    {
        public int id { get; set; }
        public string? username { get; set; }
        public string? full_name { get; set; }
        public string? role { get; set; }
        public string? student_id { get; set; }
        public string? student_name { get; set; }
    }

    public class VerifyOtpResultDto
    {
        public string? Message { get; set; }
        public string? Token { get; set; }
        public OtpUserDto? User { get; set; }
    }
}
