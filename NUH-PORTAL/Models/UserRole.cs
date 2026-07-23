using Microsoft.AspNetCore.Identity;

namespace NUH_PORTAL.Models
{
    // جدول الربط بين المستخدم والدور مع navigations — نفس فكرة الـ permit عشان الاستعلامات.
    public class UserRole : IdentityUserRole<int>
    {
        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }
}
