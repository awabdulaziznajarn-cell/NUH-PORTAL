using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Users
{
    // نتيجة بحث في الـ Active Directory (لإضافة مستخدم من الدليل).
    public class LdapUserSearchItemDto
    {
        public string samAccountName { get; set; } = string.Empty;
        public string? displayName { get; set; }
        public string? email { get; set; }
        public string? department { get; set; }
        public bool accountEnabled { get; set; }
    }

    // طلب إضافة مستخدم من الـ AD: اسم الحساب + الدور اللي هيتسند له.
    public class LdapAddUserDto
    {
        public string? username { get; set; }
        public string? role { get; set; }
        // القسم بيتحدد وقت الإضافة زي الدور بالظبط
        public Gender? scope_gender { get; set; }
    }
}
