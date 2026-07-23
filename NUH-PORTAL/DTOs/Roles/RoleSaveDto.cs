namespace NUH_PORTAL.DTOs.Roles
{
    // إنشاء/تعديل دور: الاسم (للإنشاء) + الوصف + قائمة الصلاحيات المُسندة.
    public class RoleSaveDto
    {
        public string? name { get; set; }
        public string? description { get; set; }
        public List<string> permissions { get; set; } = new();
    }
}
