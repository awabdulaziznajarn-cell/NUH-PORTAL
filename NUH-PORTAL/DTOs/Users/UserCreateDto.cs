namespace NUH_PORTAL.DTOs.Users
{
    // إنشاء مستخدم محلي (بباسورد) — الدور اختياري (الافتراضي user).
    public class UserCreateDto
    {
        public string? username { get; set; }
        public string? full_name { get; set; }
        public string? email { get; set; }
        public string? mobile { get; set; }
        public string? department { get; set; }
        public string? job_title { get; set; }
        public string? password { get; set; }
        public string? role { get; set; }
        public bool is_active { get; set; } = true;
    }
}
