using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data
{
    // Seeder للتطوير: بينشئ مستخدم لكل دور للدخول عبر الـ local fallback (من غير AD).
    // idempotent — مبيكررش لو المستخدم موجود. الباسورد للكل: DevPassword.
    public static class DbSeeder
    {
        public const string DevPassword = "Test@123";

        public static void SeedDevUsers(AppDbContext db)
        {
            var seed = new (string Username, string Role, string FullName)[]
            {
                ("admin",      "admin",      "System Admin"),
                ("cyber",      "cyber",      "Cyber Security"),
                ("supervisor", "supervisor", "Housing Supervisor"),
                ("user",       "user",       "Housing User"),
            };

            var added = false;
            foreach (var (username, role, fullName) in seed)
            {
                if (db.Users.Any(u => u.username == username))
                    continue;

                db.Users.Add(new User
                {
                    username = username,
                    full_name = fullName,
                    email = username + "@nu.edu.sa",
                    role = role,
                    is_active = true,
                    password_hash = BCrypt.Net.BCrypt.HashPassword(DevPassword),
                    created_at = DateTime.UtcNow
                });
                added = true;
            }

            if (added)
                db.SaveChanges();
        }
    }
}
