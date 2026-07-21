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

        // عينة بيانات للتطوير بس — بتتزرع مرة واحدة لو جدول الطلاب فاضي تمامًا.
        // مش بتلمس أي بيانات موجودة أبدًا.
        public static void SeedDevData(AppDbContext db)
        {
            if (db.Students.Any())
                return;

            var adminId = db.Users.FirstOrDefault(u => u.username == "admin")?.Id ?? 1;
            var now = DateTime.UtcNow;

            var students = new[]
            {
                new Student { student_id = "444100001", full_name = "أحمد محمد القحطاني", full_name_english = "Ahmed Mohammed Alqahtani", national_id = "1100000001", phone = "966550000001", gender = "male", college = "كلية الحاسب الآلي", department = "علوم الحاسب", academic_level = "3", housing_building = "40", apartment_number = "101", room_number = "1", status = "active", student_status = "active", created_at = now.AddDays(-40), created_by = adminId },
                new Student { student_id = "444100002", full_name = "خالد سعيد اليامي", full_name_english = "Khaled Saeed Alyami", national_id = "1100000002", phone = "966550000002", gender = "male", college = "كلية الهندسة", department = "مدني", academic_level = "2", housing_building = "41", apartment_number = "102", room_number = "2", status = "active", student_status = "active", created_at = now.AddDays(-35), created_by = adminId, ad_username = "h444100002", ad_status = "enabled", ad_last_sync_at = now.AddDays(-2) },
                new Student { student_id = "444100003", full_name = "سلطان فهد الوادعي", full_name_english = "Sultan Fahad Alwadei", national_id = "1100000003", phone = "966550000003", gender = "male", college = "كلية الطب", department = "طب بشري", academic_level = "5", housing_building = "42", apartment_number = "201", room_number = "1", status = "active", student_status = "active", created_at = now.AddDays(-30), created_by = adminId, ad_username = "h444100003", ad_status = "disabled", ad_last_sync_at = now.AddDays(-1) },
                new Student { student_id = "444100004", full_name = "نورة عبدالله الشهراني", full_name_english = "Noura Abdullah Alshahrani", national_id = "1100000004", phone = "966550000004", gender = "female", college = "كلية العلوم الإدارية", department = "محاسبة", academic_level = "1", housing_building = "65", apartment_number = "301", room_number = "3", status = "active", student_status = "active", created_at = now.AddDays(-25), created_by = adminId },
                new Student { student_id = "444100005", full_name = "ريم صالح عسيري", full_name_english = "Reem Saleh Asiri", national_id = "1100000005", phone = "966550000005", gender = "female", college = "كلية الصيدلة", department = "صيدلة إكلينيكية", academic_level = "4", housing_building = "66", apartment_number = "302", room_number = "1", status = "active", student_status = "active", created_at = now.AddDays(-20), created_by = adminId },
                new Student { student_id = "444100006", full_name = "عبدالرحمن علي الحارثي", full_name_english = "Abdulrahman Ali Alharthi", national_id = "1100000006", phone = "966550000006", gender = "male", college = "كلية الحاسب الآلي", department = "نظم معلومات", academic_level = "2", housing_building = "43", apartment_number = "105", room_number = "2", status = "left", student_status = "graduated", created_at = now.AddDays(-60), created_by = adminId },
                new Student { student_id = "444100007", full_name = "محمد حسن عسيري", full_name_english = "Mohammed Hassan Asiri", national_id = "1100000007", phone = "966550000007", gender = "male", college = "كلية الهندسة", department = "كهرباء", academic_level = "3", housing_building = "67", apartment_number = "106", room_number = "1", status = "active", student_status = "active", created_at = now.AddDays(-15), created_by = adminId },
                new Student { student_id = "444100008", full_name = "طالب محذوف تجريبي", full_name_english = "Deleted Test Student", national_id = "1100000008", phone = "966550000008", gender = "male", college = "كلية العلوم", department = "رياضيات", academic_level = "1", housing_building = "68", apartment_number = "107", room_number = "2", status = "active", student_status = "active", created_at = now.AddDays(-50), created_by = adminId, IsDeleted = true, DeletedBy = adminId, DeletedDate = now.AddDays(-5) },
            };
            db.Students.AddRange(students);
            db.SaveChanges();

            // طلبات تسجيل ذاتي بحالات مختلفة عشان كل صفحات الـ workflow يبان فيها حاجة
            var requests = new[]
            {
                new Request { RequestType = "self_registration", StudentId = students[0].Id, Status = "submitted", RequestNumber = "REQ-2026-000001", SubmittedBy = adminId, SubmittedAt = now.AddDays(-10), RequestedByRole = "user", RegistrationData = "{\"full_name\":\"أحمد محمد القحطاني\",\"mobile\":\"966550000001\"}" },
                new Request { RequestType = "self_registration", StudentId = students[1].Id, Status = "cyber_review", RequestNumber = "REQ-2026-000002", SubmittedBy = adminId, SubmittedAt = now.AddDays(-8), ReviewedAt = now.AddDays(-7), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-7), RequestedByRole = "supervisor" },
                new Request { RequestType = "self_registration", StudentId = students[2].Id, Status = "cyber_approved", RequestNumber = "REQ-2026-000003", SubmittedBy = adminId, SubmittedAt = now.AddDays(-6), ReviewedAt = now.AddDays(-5), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-5), CyberReviewedBy = adminId, CyberReviewedAt = now.AddDays(-4), RequestedByRole = "supervisor" },
                new Request { RequestType = "self_registration", StudentId = students[3].Id, Status = "completed", RequestNumber = "REQ-2026-000004", SubmittedBy = adminId, SubmittedAt = now.AddDays(-12), ReviewedAt = now.AddDays(-11), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-11), CyberReviewedBy = adminId, CyberReviewedAt = now.AddDays(-10), CompletedBy = adminId, CompletedAt = now.AddDays(-9), RequestedByRole = "admin" },
                new Request { RequestType = "self_registration", StudentId = students[4].Id, Status = "housing_rejected", RequestNumber = "REQ-2026-000005", SubmittedBy = adminId, SubmittedAt = now.AddDays(-3), ReviewedAt = now.AddDays(-2), ReviewedBy = adminId, HousingReviewedBy = adminId, HousingReviewedAt = now.AddDays(-2), HousingNotes = "بيانات السكن غير مكتملة", RequestedByRole = "user" },
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
                new Notification { request_id = requests[0].Id, channel = "in_app", recipient_role = "admin", message = "تم تقديم طلب جديد (REQ-2026-000001)", status = "pending", sent_at = now.AddDays(-10) },
                new Notification { request_id = requests[1].Id, channel = "in_app", recipient_role = "cyber", message = "تم إحالة الطلب (REQ-2026-000002) إلى المراجعة الإلكترونية", status = "pending", sent_at = now.AddDays(-7) },
                new Notification { request_id = requests[4].Id, channel = "in_app", recipient_role = "supervisor", message = "تم رفض الطلب (REQ-2026-000005) من قبل لجنة الإسكان", status = "pending", sent_at = now.AddDays(-2) }
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
    }
}
