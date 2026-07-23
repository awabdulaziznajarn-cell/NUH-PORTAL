namespace NUH_PORTAL.DTOs.Users
{
    // عنصر في قائمة الأدوار (dropdown إسناد الدور للمستخدم).
    public class RoleOptionDto
    {
        public string name { get; set; } = string.Empty;
        public string? description { get; set; }
    }
}
