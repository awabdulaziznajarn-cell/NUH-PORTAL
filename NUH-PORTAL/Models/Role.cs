using Microsoft.AspNetCore.Identity;

namespace NUH_PORTAL.Models
{
    // الدور = IdentityRole<int> + وصف. الصلاحيات بتترتبط بالدور كـ Role Claims (زي الـ permit).
    public class Role : IdentityRole<int>
    {
        public string? Description { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public Role() { }
        public Role(string roleName) : base(roleName) { }
    }
}
