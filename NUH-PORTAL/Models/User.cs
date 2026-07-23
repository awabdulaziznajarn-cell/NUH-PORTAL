using Microsoft.AspNetCore.Identity;

namespace NUH_PORTAL.Models
{
    // المستخدم = IdentityUser<int>. Identity بيوفّر: Id, UserName, NormalizedUserName,
    // Email, PasswordHash, PhoneNumber, SecurityStamp ... إلخ.
    // الحقول القديمة بقت من Identity: username→UserName, email→Email, password_hash→PasswordHash.
    // الأدوار بقت many-to-many عبر Identity (مش عمود role نصّي).
    public class User : IdentityUser<int>
    {
        public string? full_name { get; set; }
        public string? department { get; set; }
        public string? mobile { get; set; }
        public string? job_title { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }

        // أدوار المستخدم (navigation) — عشان نقدر نقرأ الأدوار في استعلامات EF مباشرة
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
