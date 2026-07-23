namespace NUH_PORTAL.DTOs.Roles
{
    // صف في جدول الأدوار: الاسم + الوصف + عدد المستخدمين + عدد الصلاحيات.
    public class RoleListItemDto
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public string? description { get; set; }
        public int userCount { get; set; }
        public int permissionCount { get; set; }
    }
}
