using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Users
{
    // نفس شكل الـ projection القديم بالظبط (من غير password_hash طبعًا)
    public class UserListItemDto
    {
        public int Id { get; set; }
        public string? username { get; set; }
        public string? full_name { get; set; }
        public string? email { get; set; }
        public string? role { get; set; }
        // الجوال — العمود الوحيد اللي بيهمّ في حسابات الطلاب (الدخول برمز عليه)
        public string? mobile { get; set; }
        public DateTime created_at { get; set; }
        public bool is_active { get; set; }
        // ⚠️ القفل غير التعطيل، وde كان مصدر لبس حقيقي:
        //      is_active   = قرار إداري بتاعنا (عمود عندنا في الجدول)
        //      is_locked   = قفل تلقائي من Identity بعد ٣ محاولات دخول فاشلة
        //                    (LockoutEnd) وبيفكّ لوحده بعد ١٥ دقيقة
        //    تعطيل الحساب وتفعيله تاني **مابيفكّش القفل** - عمودين مختلفين
        //    تمامًا. المسؤول كان بيعطّل ويفعّل والمستخدم لسه مش قادر يدخل،
        //    وشاشة المستخدمين مكانش فيها أي إشارة إن فيه قفل أصلًا.
        public bool is_locked { get; set; }
        // ⚠️ بيتعرض في tooltip الشارة: «مقفول لحد الساعة كذا». من غيره
        //    المسؤول مايعرفش هو يستنى ولا يفكّ.
        public DateTimeOffset? lockout_end { get; set; }
        // الحذف منطقي — الصف بيفضل موجود، فالشاشة محتاجة تعرف حالته وتاريخه
        public bool is_deleted { get; set; }
        public DateTime? deleted_at { get; set; }
        // "ad" أو "local" — الشاشة بتعرض شارة «دومين» على حسابات الدليل
        public string? auth_source { get; set; }
        // القسم (طلاب/طالبات) — بيتعرض كشارة جنب الدور
        public Gender? scope_gender { get; set; }
    }
}
