using Microsoft.AspNetCore.Identity;
using NUH_PORTAL.Core;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using System.Security.Claims;

namespace NUH_PORTAL.Data
{
    // Seeder للتطوير: بينشئ مستخدم لكل دور للدخول عبر الـ local fallback (من غير AD).
    // idempotent — مبيكررش لو المستخدم موجود. الباسورد للكل: DevPassword.
    public static class DbSeeder
    {
        public const string DevPassword = "Test@123";

        // ====================================================================
        //  الأدوار وصلاحياتها — بيانات أساسية للنظام، مش بيانات تطوير.
        //  كانت جوّه SeedDevUsersAsync اللي بتتنادى في التطوير بس، فعلى سيرفر
        //  الإنتاج جدول AspNetRoleClaims كان بيفضل فاضي. النتيجة: كل
        //  [Authorize(Policy = "...")] بيفشل -> 403 -> ولوب على صفحة الدخول
        //  بيبان للمستخدم كأنه "بيدخل ويطلع على طول".
        //  الميثود دي idempotent وبتتنادى من Program.cs في كل البيئات.
        // ====================================================================
        public static async Task SeedRolesAndPermissionsAsync(RoleManager<Role> roleManager, ILogger? logger = null)
        {
            // الأدوار الأساسية
            foreach (var role in new[] { "admin", "cyber", "supervisor", "user" })
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new Role(role) { Description = role });

            // ترقية صلاحيات الأدوار الموجودة — قبل أي إسناد، عشان الدور اللي اتعدّل
            // بإيد المسؤول مايتحسبش "فاضي" فيترجّع للافتراضي.
            await UpgradeRolePermissionsAsync(roleManager, logger);

            // admin: الصلاحيات **الجديدة** بس (شوف GrantNewPermissionsToAdminAsync).
            await GrantNewPermissionsToAdminAsync(roleManager, logger);

            // ⚠️ باقي الأدوار: الافتراضي بيتزرع أول مرة بس (لما الدور يبقى بلا أي صلاحية).
            //    قبل كده كل إعادة تشغيل كانت بترجّع الصلاحيات اللي المسؤول شالها بإيده
            //    من شاشة الأدوار، فالتعديل بيتلغي لوحده بعد أول deploy ومحدش واخد باله.
            await SeedDefaultsIfEmptyAsync(roleManager, "supervisor", new[]
            {
                "students.view", "students.create", "students.bulkImport", "students.edit", "students.changeStatus",
                "requests.view", "requests.create", "requests.reviewHousing",
                "housing.view", "housing.transfer",
                "lookups.manage", "auditLogs.view", "reports.view"
            }, logger);
            await SeedDefaultsIfEmptyAsync(roleManager, "cyber", new[]
            {
                "requests.view", "requests.reviewCyber",
                "housing.view", "auditLogs.view", "errorLogs.view"
            }, logger);
            // دور الطالب (OTP) — بدون صلاحيات موظفين. كان requests.view وده كان بيخلّي توكن الطالب
            // يوصل endpoints المفروض للموظفين؛ الطالب بيتابع طلبه عبر /api/Registration و /api/RequestTracking.
        }

        // ====================================================================
        //  ترحيل الصلاحيات القديمة (students.manage / requests.process / housing.manage)
        //  للصلاحيات المفصّلة. من غير الترحيل ده الأدوار الموجودة بتفقد صلاحياتها
        //  فجأة بعد أول deploy — الصلاحية القديمة مابقاش ليها policy، والجديدة
        //  محدش مداهاش لحد.
        //
        //  ⚠️ الترحيل مشروط بالدور عن قصد: requests.process كانت الكنترولر بيفلتر
        //     معاها بالدور كمان، فلو وسّعناها للكل هيبقى المشرف يقدر يراجع مرحلة
        //     الأمن السيبراني والعكس — ده توسيع صلاحية مش ترحيل.
        // ====================================================================
        private static readonly string[] LegacyPermissions = { "students.manage", "requests.process", "housing.manage" };

        // ⚠️ في الكود القديم كانت في إجراءات مقفولة بـ [Authorize(Roles = "...")] من غير
        //    أي صلاحية تقابلها، يعني مفيش claim نرحّل منه أصلاً. أشهرها نقل السكن:
        //    كان Roles="supervisor,admin" وخلاص. من غير الإضافة دي المشرف يصحى يلاقي
        //    أهم إجراء عنده بيرجّع «غير مصرح» وإحنا مش عارفين ليه.
        //    بتتنفّذ مرة واحدة بس (بعلامة النسخة تحت)، فلو المسؤول شالها بإيده مابترجعش.
        private static readonly Dictionary<string, string[]> RoleOnlyCapabilities = new(StringComparer.OrdinalIgnoreCase)
        {
            ["supervisor"] = new[] { "housing.transfer", "students.bulkImport" }
        };

        // علامة على الدور إن صلاحياته اتعملها ترقية للنسخة دي. النوع مختلف عن
        // "permission" عن قصد: شاشة الأدوار بتمسح وتضيف claims من نوع permission بس،
        // فالعلامة بتفضل حتى بعد ما المسؤول يعدّل صلاحيات الدور.
        private const string SchemaClaimType = "permission_schema";
        private const string SchemaVersion = "2";

        private static string[] MapLegacy(string legacy, string roleName) => (legacy, roleName.ToLowerInvariant()) switch
        {
            // students.manage كانت بتغطّي الأربعة دول بالظبط في الكود القديم، فالتوسيع مش زيادة صلاحية
            ("students.manage", "admin") => new[] { "students.create", "students.bulkImport", "students.edit", "students.delete", "students.changeStatus", "students.overrideStatus" },
            ("students.manage", _) => new[] { "students.create", "students.bulkImport", "students.edit", "students.changeStatus" },

            ("requests.process", "admin") => new[] { "requests.create", "requests.reviewHousing", "requests.reviewCyber", "requests.complete" },
            ("requests.process", "supervisor") => new[] { "requests.create", "requests.reviewHousing" },
            ("requests.process", "cyber") => new[] { "requests.reviewCyber" },
            // دور مخصّص: بناخد المجموعة الآمنة بس وبنحذّر في السجل — الأفضل إن
            // المسؤول يحدّد بنفسه مرحلة المراجعة اللي الدور ده مسؤول عنها.
            ("requests.process", _) => new[] { "requests.create" },

            ("housing.manage", "admin") => new[] { "housing.transfer", "housing.manageAccounts", "housing.syncAd" },
            ("housing.manage", _) => new[] { "housing.transfer" },

            _ => Array.Empty<string>()
        };

        private static async Task UpgradeRolePermissionsAsync(RoleManager<Role> roleManager, ILogger? logger)
        {
            foreach (var role in roleManager.Roles.ToList())
            {
                var claims = await roleManager.GetClaimsAsync(role);

                // اتعملت قبل كده؟ خلاص — مانلمسش الدور تاني مهما اتعدّل بعدها.
                if (claims.Any(c => c.Type == SchemaClaimType && c.Value == SchemaVersion))
                    continue;

                var current = claims.Where(c => c.Type == ClaimConstants.Permission)
                                    .Select(c => c.Value)
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var legacy in LegacyPermissions)
                {
                    if (!current.Contains(legacy)) continue;

                    var replacements = MapLegacy(legacy, role.Name ?? "");
                    foreach (var p in replacements)
                        if (current.Add(p))
                            await roleManager.AddClaimAsync(role, new Claim(ClaimConstants.Permission, p));

                    foreach (var stale in claims.Where(c => c.Type == ClaimConstants.Permission
                                                        && string.Equals(c.Value, legacy, StringComparison.OrdinalIgnoreCase)))
                        await roleManager.RemoveClaimAsync(role, stale);

                    current.Remove(legacy);

                    logger?.LogInformation("Permission migration: role {Role}: {Legacy} -> {New}",
                        role.Name, legacy, string.Join(", ", replacements));

                    if (role.Name is not ("admin" or "supervisor" or "cyber"))
                        logger?.LogWarning(
                            "Role {Role} had the legacy permission {Legacy}. It was migrated to the safe subset only ({New}). Open the Roles screen and grant the review permissions this role actually needs.",
                            role.Name, legacy, string.Join(", ", replacements));
                }

                // الإجراءات اللي كانت بالدور من غير صلاحية تقابلها
                if (role.Name != null && RoleOnlyCapabilities.TryGetValue(role.Name, out var extras))
                {
                    foreach (var p in extras)
                        if (current.Add(p))
                        {
                            await roleManager.AddClaimAsync(role, new Claim(ClaimConstants.Permission, p));
                            logger?.LogInformation("Role {Role}: granted {Permission} (was role-based before, no claim to migrate from).", role.Name, p);
                        }
                }

                await roleManager.AddClaimAsync(role, new Claim(SchemaClaimType, SchemaVersion));
            }
        }

        // ====================================================================
        //  صلاحيات دور admin.
        //
        //  ⚠️ الغلط اللي كان هنا: السطر كان
        //        AssignRolePermissionsAsync(roleManager, "admin", ApplicationPermissions.All)
        //     يعني **كل** الصلاحيات بتترجّع لـ admin مع كل إقلاع للتطبيق.
        //     والميثود دي بتتنادى من Program.cs في كل البيئات، والإقلاع مش
        //     بيحصل مع النشر بس: IIS بيعيد تدوير الـ application pool لوحده
        //     (بعد خمول، وكل ٢٩ ساعة افتراضيًّا). فمدير النظام بيشيل صلاحية
        //     من شاشة الأدوار، ويلاقيها رجعت «لوحدها» بعد شوية بلا أي أثر
        //     ولا سبب ظاهر.
        //
        //  ⚠️ ونفس العطل ده كان مكتشَف ومتصلَّح لباقي الأدوار: supervisor و
        //     cyber بياخدوا الافتراضي أول مرة بس (SeedDefaultsIfEmptyAsync)،
        //     والتعليق فوقها بيقول الكلام ده بالنص. admin بس هو اللي كان
        //     مستثنى — والاستثناء كان له سبب حقيقي مش مجرد سهو:
        //
        //         لو صلاحية جديدة اتضافت في نسخة أحدث، ومحدش مداهاش لحد،
        //         الشاشة الجديدة بتفضل مقفولة على **الكل** بما فيهم مدير
        //         النظام نفسه — فمفيش حد يقدر يفتحها لأي حد.
        //
        //  الحل إن الـ seeder يفرّق بين «صلاحية جديدة على النظام» و«صلاحية
        //  اتشالت بإيد». الفرق ده مش موجود في البيانات (الاتنين = مفتاح في
        //  الكود مش موجود على الدور)، فبنسجّله: علامة على الدور فيها المفاتيح
        //  اللي الـ seeder شافها قبل كده. الجديد = اللي مش في العلامة.
        //
        //  ⚠️ أول تشغيل بعد التعديل ده: مفيش علامة لسه. لو admin عنده صلاحيات
        //     فعلًا، يبقى ده نظام شغّال — بنسجّل المفاتيح الحالية كلها على إنها
        //     «اتعرضت» و**ما نمنحش حاجة**، وإلا كنا هنرجّع اللي المسؤول شاله
        //     مرة أخيرة وهو بالظبط اللي بنصلّحه. أما لو admin بلا أي صلاحية،
        //     يبقى تنصيب جديد وبياخد الكل.
        // ====================================================================
        private static async Task GrantNewPermissionsToAdminAsync(RoleManager<Role> roleManager, ILogger? logger)
        {
            var role = await roleManager.FindByNameAsync("admin");
            if (role == null) return;

            var claims  = await roleManager.GetClaimsAsync(role);
            var granted = claims.Where(c => c.Type == ClaimConstants.Permission).Select(c => c.Value).ToHashSet();
            var marker  = claims.FirstOrDefault(c => c.Type == ClaimConstants.SeededPermissions);

            var all = ApplicationPermissions.All.Select(p => p.Value).ToArray();

            HashSet<string> known;
            if (marker != null)
            {
                known = marker.Value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            }
            else if (granted.Count > 0)
            {
                // نظام شغّال بلا علامة — كل المفاتيح الحالية تُعتبر «اتعرضت»
                known = all.ToHashSet();
                logger?.LogInformation(
                    "Role admin: first run with seed marker. Treating the {Count} existing permission keys as already offered; nothing re-granted.",
                    all.Length);
            }
            else
            {
                // تنصيب جديد: الدور فاضي تمامًا
                known = new HashSet<string>();
            }

            var fresh = all.Where(p => !known.Contains(p)).ToArray();
            if (fresh.Length > 0)
            {
                await AssignRolePermissionsAsync(roleManager, "admin", fresh);
                // Warning مش Information: منح صلاحية من غير ما حد يطلبها حدث
                // يستاهل يتشاف في السجل، مش سطر روتيني.
                logger?.LogWarning("Role admin: granted {Count} newly added permission(s): {Permissions}",
                    fresh.Length, string.Join(", ", fresh));
            }

            // ⚠️ العلامة بتتحدّث دايمًا حتى لو مفيش جديد: لازم تعكس مفاتيح
            //    النسخة الحالية بالظبط، وإلا مفتاح اتشال من الكود يفضل محسوب.
            var updated = string.Join(',', all.OrderBy(p => p, StringComparer.Ordinal));
            if (marker == null || marker.Value != updated)
            {
                if (marker != null) await roleManager.RemoveClaimAsync(role, marker);
                await roleManager.AddClaimAsync(role, new Claim(ClaimConstants.SeededPermissions, updated));
            }
        }

        // بيزرع الصلاحيات الافتراضية للدور لو الدور لسه بلا أي صلاحية خالص.
        // لو المسؤول عدّل الدور، بنسيبه زي ما هو.
        private static async Task SeedDefaultsIfEmptyAsync(RoleManager<Role> roleManager, string roleName, string[] permissions, ILogger? logger)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null) return;

            var hasAny = (await roleManager.GetClaimsAsync(role)).Any(c => c.Type == ClaimConstants.Permission);
            if (hasAny)
            {
                logger?.LogInformation("Role {Role} already has permissions - defaults not re-applied.", roleName);
                return;
            }

            await AssignRolePermissionsAsync(roleManager, roleName, permissions);
            logger?.LogInformation("Role {Role} seeded with {Count} default permissions.", roleName, permissions.Length);
        }

        public static async Task SeedDevUsersAsync(UserManager<User> userManager, RoleManager<Role> roleManager)
        {
            var seed = new (string Username, string Role, string FullName)[]
            {
                ("admin",      "admin",      "System Admin"),
                ("cyber",      "cyber",      "Cyber Security"),
                ("supervisor", "supervisor", "Housing Supervisor"),
                ("user",       "user",       "Housing User"),
            };

            await SeedRolesAndPermissionsAsync(roleManager);

            // المستخدمون (بهاشر Identity — الباسورد للكل DevPassword)
            foreach (var (username, role, fullName) in seed)
            {
                // مهم: المستخدم الموجود مش بيتخطّى — بنتأكد إنه لسه في دوره.
                // لو الدور اتشال (أو الصف اتمسح من AspNetUserRoles) المستخدم بيدخل
                // بصفر صلاحيات وكل صفحة بترفضه، وده كان بيتفسّر غلط كمشكلة جلسة.
                var existingUser = await userManager.FindByNameAsync(username);
                if (existingUser != null)
                {
                    if (!await userManager.IsInRoleAsync(existingUser, role))
                        await userManager.AddToRoleAsync(existingUser, role);
                    continue;
                }

                var user = new User
                {
                    UserName = username,
                    full_name = fullName,
                    Email = username + "@nu.edu.sa",
                    is_active = true,
                    created_at = DateTime.UtcNow
                };
                var res = await userManager.CreateAsync(user, DevPassword);
                if (res.Succeeded)
                    await userManager.AddToRoleAsync(user, role);
            }

            // مستخدم dev — سوبر يوزر بكل الأدوار وبالتالي كل الصلاحيات (dev / Test@123).
            // بياخد الأدوار الأربعة؛ دور admin فيه كل الـ permissions فالكوكي بيتحمّل بكل الصلاحيات عند الدخول.
            if (await userManager.FindByNameAsync("dev") == null)
            {
                var dev = new User
                {
                    UserName = "dev",
                    full_name = "Developer (Super User)",
                    Email = "dev@nu.edu.sa",
                    is_active = true,
                    created_at = DateTime.UtcNow
                };
                var devRes = await userManager.CreateAsync(dev, DevPassword);
                if (devRes.Succeeded)
                    await userManager.AddToRolesAsync(dev, new[] { "admin", "supervisor", "cyber", "user" });
            }
        }

        // إسناد صلاحيات لدور (idempotent — مبيكررش claim موجود)
        private static async Task AssignRolePermissionsAsync(RoleManager<Role> roleManager, string roleName, string[] permissions)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null) return;

            var existing = (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == ClaimConstants.Permission)
                .Select(c => c.Value)
                .ToHashSet();

            foreach (var p in permissions)
                if (!existing.Contains(p))
                    await roleManager.AddClaimAsync(role, new Claim(ClaimConstants.Permission, p));
        }

        // عينة بيانات للتطوير بس — بتتزرع مرة واحدة لو جدول الطلاب فاضي تمامًا.
        // مش بتلمس أي بيانات موجودة أبدًا.
        public static void SeedDevData(AppDbContext db)
        {
            if (db.Students.Any())
                return;

            var adminId = db.Users.FirstOrDefault(u => u.UserName == "admin")?.Id ?? 1;
            var now = DateTime.UtcNow;

            var students = new[]
            {
                new Student { student_id = "444100001", full_name = "أحمد محمد القحطاني", full_name_english = "Ahmed Mohammed Alqahtani", national_id = "1100000001", phone = "966550000001", gender = Gender.Male, college = "كلية الحاسب الآلي", department = "علوم الحاسب", academic_level = "3", housing_building = "40", apartment_number = "101", room_number = "1", status = StudentState.active, student_status = StudentStatus.active, created_at = now.AddDays(-40), created_by = adminId },
                new Student { student_id = "444100002", full_name = "خالد سعيد اليامي", full_name_english = "Khaled Saeed Alyami", national_id = "1100000002", phone = "966550000002", gender = Gender.Male, college = "كلية الهندسة", department = "مدني", academic_level = "2", housing_building = "41", apartment_number = "102", room_number = "2", status = StudentState.active, student_status = StudentStatus.active, created_at = now.AddDays(-35), created_by = adminId, ad_username = "h444100002", ad_status = AdStatus.enabled, ad_last_sync_at = now.AddDays(-2) },
                new Student { student_id = "444100003", full_name = "سلطان فهد الوادعي", full_name_english = "Sultan Fahad Alwadei", national_id = "1100000003", phone = "966550000003", gender = Gender.Male, college = "كلية الطب", department = "طب بشري", academic_level = "5", housing_building = "42", apartment_number = "201", room_number = "1", status = StudentState.active, student_status = StudentStatus.active, created_at = now.AddDays(-30), created_by = adminId, ad_username = "h444100003", ad_status = AdStatus.disabled, ad_last_sync_at = now.AddDays(-1) },
                new Student { student_id = "444100004", full_name = "نورة عبدالله الشهراني", full_name_english = "Noura Abdullah Alshahrani", national_id = "1100000004", phone = "966550000004", gender = Gender.Female, college = "كلية العلوم الإدارية", department = "محاسبة", academic_level = "1", housing_building = "65", apartment_number = "301", room_number = "3", status = StudentState.active, student_status = StudentStatus.active, created_at = now.AddDays(-25), created_by = adminId },
                new Student { student_id = "444100005", full_name = "ريم صالح عسيري", full_name_english = "Reem Saleh Asiri", national_id = "1100000005", phone = "966550000005", gender = Gender.Female, college = "كلية الصيدلة", department = "صيدلة إكلينيكية", academic_level = "4", housing_building = "66", apartment_number = "302", room_number = "1", status = StudentState.active, student_status = StudentStatus.active, created_at = now.AddDays(-20), created_by = adminId },
                new Student { student_id = "444100006", full_name = "عبدالرحمن علي الحارثي", full_name_english = "Abdulrahman Ali Alharthi", national_id = "1100000006", phone = "966550000006", gender = Gender.Male, college = "كلية الحاسب الآلي", department = "نظم معلومات", academic_level = "2", housing_building = "43", apartment_number = "105", room_number = "2", status = StudentState.left, student_status = StudentStatus.graduated, created_at = now.AddDays(-60), created_by = adminId },
                new Student { student_id = "444100007", full_name = "محمد حسن عسيري", full_name_english = "Mohammed Hassan Asiri", national_id = "1100000007", phone = "966550000007", gender = Gender.Male, college = "كلية الهندسة", department = "كهرباء", academic_level = "3", housing_building = "67", apartment_number = "106", room_number = "1", status = StudentState.active, student_status = StudentStatus.active, created_at = now.AddDays(-15), created_by = adminId },
                new Student { student_id = "444100008", full_name = "طالب محذوف تجريبي", full_name_english = "Deleted Test Student", national_id = "1100000008", phone = "966550000008", gender = Gender.Male, college = "كلية العلوم", department = "رياضيات", academic_level = "1", housing_building = "68", apartment_number = "107", room_number = "2", status = StudentState.active, student_status = StudentStatus.active, created_at = now.AddDays(-50), created_by = adminId, IsDeleted = true, DeletedBy = adminId, DeletedDate = now.AddDays(-5) },
            };
            db.Students.AddRange(students);
            db.SaveChanges();

            // طلبات تسجيل ذاتي بحالات مختلفة عشان كل صفحات الـ workflow يبان فيها حاجة
            var requests = new[]
            {
                new Request { RequestType = RequestType.self_registration, StudentId = students[0].Id, Status = "submitted", RequestNumber = "REQ-2026-000001", SubmittedBy = adminId, SubmittedAt = now.AddDays(-10), RequestedByRole = "user", RegistrationData = "{\"full_name\":\"أحمد محمد القحطاني\",\"mobile\":\"966550000001\"}" },
                new Request { RequestType = RequestType.self_registration, StudentId = students[1].Id, Status = "cyber_review", RequestNumber = "REQ-2026-000002", SubmittedBy = adminId, SubmittedAt = now.AddDays(-8), ReviewedAt = now.AddDays(-7), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-7), RequestedByRole = "supervisor" },
                new Request { RequestType = RequestType.self_registration, StudentId = students[2].Id, Status = "cyber_approved", RequestNumber = "REQ-2026-000003", SubmittedBy = adminId, SubmittedAt = now.AddDays(-6), ReviewedAt = now.AddDays(-5), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-5), CyberReviewedBy = adminId, CyberReviewedAt = now.AddDays(-4), RequestedByRole = "supervisor" },
                new Request { RequestType = RequestType.self_registration, StudentId = students[3].Id, Status = "completed", RequestNumber = "REQ-2026-000004", SubmittedBy = adminId, SubmittedAt = now.AddDays(-12), ReviewedAt = now.AddDays(-11), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-11), CyberReviewedBy = adminId, CyberReviewedAt = now.AddDays(-10), CompletedBy = adminId, CompletedAt = now.AddDays(-9), RequestedByRole = "admin" },
                new Request { RequestType = RequestType.self_registration, StudentId = students[4].Id, Status = "housing_rejected", RequestNumber = "REQ-2026-000005", SubmittedBy = adminId, SubmittedAt = now.AddDays(-3), ReviewedAt = now.AddDays(-2), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-2), HousingNotes = "بيانات السكن غير مكتملة", RequestedByRole = "user" },
            };
            db.Requests.AddRange(requests);
            db.SaveChanges();

            // سجل workflow لطلب واحد عشان صفحة التتبع والتاريخ
            db.WorkflowHistories.AddRange(
                new WorkflowHistory { RequestId = requests[1].Id, FromStage = null, ToStage = "submitted", ActionBy = adminId, ActionDate = now.AddDays(-8), Notes = "تقديم الطلب" },
                new WorkflowHistory { RequestId = requests[1].Id, FromStage = "submitted", ToStage = "cyber_review", ActionBy = adminId, ActionDate = now.AddDays(-7), Notes = "موافقة الإسكان" }
            );

            // إشعارات معلقة للأدوار
            db.Notifications.AddRange(
                new Notification { request_id = requests[0].Id, channel = "in_app", recipient_role = "admin", message = "تم تقديم طلب جديد (REQ-2026-000001)", status = NotificationStatus.pending, sent_at = now.AddDays(-10) },
                new Notification { request_id = requests[1].Id, channel = "in_app", recipient_role = "cyber", message = "تم إحالة الطلب (REQ-2026-000002) إلى المراجعة الإلكترونية", status = NotificationStatus.pending, sent_at = now.AddDays(-7) },
                new Notification { request_id = requests[4].Id, channel = "in_app", recipient_role = "supervisor", message = "تم رفض الطلب (REQ-2026-000005) من قبل لجنة الإسكان", status = NotificationStatus.pending, sent_at = now.AddDays(-2) }
            );

            // إجراء مغادرة + سجل دورة حياة للطالب المتخرج
            var action = new StudentStatusAction { StudentId = students[5].Id, StudentNumber = students[5].student_id, StatusType = "graduated", Notes = "تخرج من الكلية بنهاية الفصل", CreatedBy = adminId, CreatedDate = now.AddDays(-14) };
            db.StudentStatusActions.Add(action);

            db.AccountLifecycleLogs.AddRange(
                new AccountLifecycleLog { StudentId = students[5].Id, Action = "disabled", PerformedBy = adminId, PerformedAt = now.AddDays(-14), Details = "Admin status change - تخرج من الكلية: تخرج من الكلية بنهاية الفصل (لا يوجد حساب شبكة)", IpAddress = "127.0.0.1" },
                new AccountLifecycleLog { StudentId = students[2].Id, Action = "disabled", PerformedBy = adminId, PerformedAt = now.AddDays(-1), Details = "AD account disabled by admin", IpAddress = "127.0.0.1" }
            );

            // نقل سكن واحد
            db.HousingTransfers.Add(new HousingTransfer
            {
                StudentId = students[0].Id, StudentNumber = students[0].student_id,
                OldBuilding = "40", OldApartment = "101", OldRoom = "1",
                NewBuilding = "43", NewApartment = "104", NewRoom = "2",
                Reason = "housing_issue", CreatedBy = adminId, CreatedAt = now.AddDays(-4)
            });

            db.SaveChanges();
        }

        // زرع القوائم المرجعية (lookups) — بيانات أساسية في كل البيئات. idempotent (بيتخطّى الموجود بالكود).
        public static void SeedLookups(AppDbContext db)
        {
            var colleges = new (string Code, string Ar, string En, int Order)[]
            {
                ("engineering", "الهندسة", "Engineering", 1),
                ("medicine", "الطب", "Medicine", 2),
                ("cs", "علوم الحاسب والمعلومات", "Computer & Information Sciences", 3),
                ("science", "العلوم", "Science", 4),
                ("business", "إدارة الأعمال", "Business Administration", 5),
                ("arts", "الآداب والعلوم الإنسانية", "Arts & Humanities", 6),
                ("education", "التربية", "Education", 7),
                ("pharmacy", "الصيدلة", "Pharmacy", 8),
            };
            foreach (var (code, ar, en, order) in colleges)
                if (!db.Colleges.Any(c => c.Code == code))
                    db.Colleges.Add(new College { Code = code, ArName = ar, EnName = en, DisplayOrder = order, IsActive = true });
            db.SaveChanges();

            var deps = new (string Code, string Ar, string En, int Order, string CollegeCode)[]
            {
                ("cs", "علوم الحاسب والمعلومات", "Computer & Information Sciences", 1, "cs"),
                ("computer", "هندسة الحاسب", "Computer Engineering", 2, "engineering"),
                ("electrical", "الهندسة الكهربائية", "Electrical Engineering", 3, "engineering"),
                ("mechanical", "الهندسة الميكانيكية", "Mechanical Engineering", 4, "engineering"),
                ("civil", "الهندسة المدنية", "Civil Engineering", 5, "engineering"),
                ("math", "الرياضيات", "Mathematics", 6, "science"),
                ("physics", "الفيزياء", "Physics", 7, "science"),
                ("chemistry", "الكيمياء", "Chemistry", 8, "science"),
                ("biology", "الأحياء", "Biology", 9, "science"),
                ("business", "إدارة الأعمال", "Business Administration", 10, "business"),
                ("accounting", "المحاسبة", "Accounting", 11, "business"),
                ("islamic", "الدراسات الإسلامية", "Islamic Studies", 12, "arts"),
                ("arabic", "اللغة العربية", "Arabic Language", 13, "arts"),
                ("english", "اللغة الإنجليزية", "English Language", 14, "arts"),
            };
            foreach (var (code, ar, en, order, collegeCode) in deps)
                if (!db.Departments.Any(d => d.Code == code))
                {
                    var collegeId = db.Colleges.FirstOrDefault(c => c.Code == collegeCode)?.Id;
                    db.Departments.Add(new Department { Code = code, ArName = ar, EnName = en, DisplayOrder = order, IsActive = true, CollegeId = collegeId });
                }
            db.SaveChanges();

            // توزيع المباني المعتمد: 65-70 بنين، 40-43 بنات.
            // كان مقلوبًا (40-43 و65-67 بنين، 68-70 بنات) فكانت شاشة التسجيل
            // بتفلتر صح والقائمة الراجعة من الداتابيز غلط.
            // ملاحظة: SeedLookups بيتخطّى المبنى الموجود بالكود، فالصفوف القديمة
            // بتتصحّح بسكربت FixBuildings.sql مرة واحدة — الزرع هنا للقواعد الجديدة.
            // ⚠️ السعة وأسلوب الترقيم جزء من صفّ المبنى لا افتراض في الكود
            //    (اتصال إدارة الإسكان): ٦٦ و٦٨ و٦٩ و٧٠ غرفها بتلاتة، و٦٥ و٦٧
            //    باتنين والمشرف يقدر يحطّ تالت، وسكن الطالبات باتنين والمشرفة
            //    تقدر تزوّد تالتة. والترقيم متّصل في سكن الطلاب وبيبدأ من أول
            //    كل دور في سكن الطالبات - الشرح في Models/Enums/HousingNumbering.
            var buildings = new (string Code, Gender Gender, int Cap, int CapMax, HousingNumbering Numbering)[]
            {
                ("65", Gender.Male, 2, 3, HousingNumbering.Continuous),
                ("66", Gender.Male, 3, 3, HousingNumbering.Continuous),
                ("67", Gender.Male, 2, 3, HousingNumbering.Continuous),
                ("68", Gender.Male, 3, 3, HousingNumbering.Continuous),
                ("69", Gender.Male, 3, 3, HousingNumbering.Continuous),
                ("70", Gender.Male, 3, 3, HousingNumbering.Continuous),
                ("40", Gender.Female, 2, 3, HousingNumbering.PerFloor),
                ("41", Gender.Female, 2, 3, HousingNumbering.PerFloor),
                ("42", Gender.Female, 2, 3, HousingNumbering.PerFloor),
                ("43", Gender.Female, 2, 3, HousingNumbering.PerFloor),
            };
            for (var i = 0; i < buildings.Length; i++)
                if (!db.Buildings.Any(x => x.Code == buildings[i].Code))
                    db.Buildings.Add(new Building
                    {
                        Code = buildings[i].Code,
                        ArName = "مبنى " + buildings[i].Code,
                        EnName = "Building " + buildings[i].Code,
                        Gender = buildings[i].Gender,
                        RoomCapacity = buildings[i].Cap,
                        RoomCapacityMax = buildings[i].CapMax,
                        Numbering = buildings[i].Numbering,
                        DisplayOrder = i + 1,
                        IsActive = true
                    });
            db.SaveChanges();

            var levels = new (string Code, string Ar, string En)[]
            {
                ("1", "المستوى الأول", "Level 1"),
                ("2", "المستوى الثاني", "Level 2"),
                ("3", "المستوى الثالث", "Level 3"),
                ("4", "المستوى الرابع", "Level 4"),
                ("5", "المستوى الخامس", "Level 5"),
            };
            for (var i = 0; i < levels.Length; i++)
                if (!db.AcademicLevels.Any(x => x.Code == levels[i].Code))
                    db.AcademicLevels.Add(new AcademicLevel { Code = levels[i].Code, ArName = levels[i].Ar, EnName = levels[i].En, DisplayOrder = i + 1, IsActive = true });
            db.SaveChanges();

            if (!db.Terms.Any())
            {
                db.Terms.AddRange(
                    new Term { ArText = "ألتزم بأنظمة ولوائح السكن الجامعي.", EnText = "I commit to the university housing regulations.", DisplayOrder = 1, IsActive = true },
                    new Term { ArText = "أتعهّد بالمحافظة على ممتلكات السكن وتجهيزاته.", EnText = "I pledge to preserve the housing property and facilities.", DisplayOrder = 2, IsActive = true },
                    new Term { ArText = "أوافق على مغادرة السكن عند انتهاء صفتي كطالب.", EnText = "I agree to vacate the housing upon losing my student status.", DisplayOrder = 3, IsActive = true }
                );
                db.SaveChanges();
            }
        }

        // ترحيل محافظ لقيم الطلاب النصية إلى مفاتيح القوائم — يطابق الكود أو الاسم المعروف فقط،
        // وأي قيمة غير متطابقة تبقى null (العمود النصي القديم محفوظ، ويقدر الأدمن يظبّطها من شاشة التعديل).
        public static void BackfillStudentLookups(AppDbContext db)
        {
            var students = db.Students
                .Where(s => (s.CollegeId == null && s.college != null)
                         || (s.DepartmentId == null && s.department != null)
                         || (s.BuildingId == null && s.housing_building != null)
                         || (s.AcademicLevelId == null && s.academic_level != null))
                .ToList();
            if (students.Count == 0) return;

            var colleges = db.Colleges.ToList();
            var deps = db.Departments.ToList();
            var buildings = db.Buildings.ToList();
            var levels = db.AcademicLevels.ToList();

            static bool Same(string? a, string b) => !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);
            // مطابقة كلية: بالكود أو الاسم العربي/الإنجليزي أو "كلية " + الاسم العربي
            int? MatchCollege(string? v) => colleges.FirstOrDefault(c => Same(v, c.Code) || (v != null && (v.Trim() == c.ArName || v.Trim() == "كلية " + c.ArName)) || Same(v, c.EnName))?.Id;
            int? MatchDept(string? v) => deps.FirstOrDefault(d => Same(v, d.Code) || (v != null && v.Trim() == d.ArName) || Same(v, d.EnName))?.Id;
            int? MatchBuilding(string? v) => buildings.FirstOrDefault(b => Same(v, b.Code) || (v != null && v.Trim() == b.ArName))?.Id;
            int? MatchLevel(string? v) => levels.FirstOrDefault(l => Same(v, l.Code) || (v != null && v.Trim() == l.ArName))?.Id;

            foreach (var s in students)
            {
                if (s.CollegeId == null) s.CollegeId = MatchCollege(s.college);
                if (s.DepartmentId == null) s.DepartmentId = MatchDept(s.department);
                if (s.BuildingId == null) s.BuildingId = MatchBuilding(s.housing_building);
                if (s.AcademicLevelId == null) s.AcademicLevelId = MatchLevel(s.academic_level);
            }
            db.SaveChanges();
        }
    }
}
