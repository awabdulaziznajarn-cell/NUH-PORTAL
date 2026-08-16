using Microsoft.AspNetCore.Identity;
using NUH_PORTAL.Models.Enums;

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

        // ⚠️ حذف منطقي مش نهائي — والسبب مش تردّد.
        //    جدول AuditLogs بيربط الفاعل بالـ id بس، مافيهوش عمود اسم، والعلاقة
        //    معمولة OnDelete: SetNull. فالحذف النهائي كان هيحوّل الفاعل في كل
        //    إجراء عمله الموظف من أول ما اشتغل إلى NULL — السجل يفضل موجود من
        //    غير إجابة على «مين عمل ده؟». وكمان Requests.submitted_by عمود رقم
        //    من غير علاقة، فكان هيفضل مشاور على صف مش موجود.
        public bool is_deleted { get; set; }
        public DateTime? deleted_at { get; set; }
        public int? deleted_by { get; set; }

        // "ad" = الحساب جاي من الدليل (اتسحب من شاشة المستخدمين أو اتعمل تلقائيًا
        //        عند أول دخول بحساب الجامعة) — الباسورد عند الدومين مش عندنا.
        // "local" = حساب محلي بباسورد متخزّن في النظام.
        // بيتعرض كشارة جنب اسم المستخدم عشان المسؤول يفرّق بين الاتنين من الشاشة.
        public string? auth_source { get; set; }

        // القسم اللي الموظف مسؤول عنه: طلاب أو طالبات. فاضي = بلا تقييد.
        // ⚠️ ليه على المستخدم مش على الدور: المشرف والمشرفة بنفس الدور بالظبط
        //    (supervisor) وبنفس الصلاحيات — اللي بيفرّق بينهم هو القسم اللي كل
        //    واحد مسؤول عنه، وده صفة الشخص. لو عملناها دورين، كل صلاحية جديدة
        //    هتتظبط مرتين وأول مرة ننسى واحدة الدورين يختلفوا ومحدش ياخد باله.
        //    ومين يتخطّى التقييد ده بيتحدد من صلاحية students.allGenders على الدور.
        public Gender? scope_gender { get; set; }

        // أدوار المستخدم (navigation) — عشان نقدر نقرأ الأدوار في استعلامات EF مباشرة
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
