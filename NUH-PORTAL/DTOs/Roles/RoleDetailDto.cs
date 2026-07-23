namespace NUH_PORTAL.DTOs.Roles
{
    // تفاصيل دور لفورم التعديل: الصلاحيات المُسندة (قيم permission).
    public class RoleDetailDto
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public string? description { get; set; }
        public List<string> permissions { get; set; } = new();
    }
}
