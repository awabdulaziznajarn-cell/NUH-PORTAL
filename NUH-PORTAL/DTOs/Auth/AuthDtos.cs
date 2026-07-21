namespace NUH_PORTAL.DTOs.Auth
{
    public class LoginRequest
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    public class SetPasswordRequest
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    // نفس مفاتيح الرد القديمة بالحرف
    public class AuthUserDto
    {
        public int id { get; set; }
        public string? username { get; set; }
        public string? full_name { get; set; }
        public string? role { get; set; }
    }

    public class LoginResultDto
    {
        public string? Message { get; set; }
        public string? Token { get; set; }
        public AuthUserDto? User { get; set; }
    }
}
