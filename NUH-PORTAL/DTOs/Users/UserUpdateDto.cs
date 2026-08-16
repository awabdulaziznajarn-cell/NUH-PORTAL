using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Users
{
    // تعديل مستخدم — اسم المستخدم (UserName) ثابت لأنه مفتاح الدخول.
    // password اختياري: لو اتبعت اتعيّن باسورد جديد، وإلا يفضل زي ما هو.
    public class UserUpdateDto
    {
        public string? full_name { get; set; }
        public string? email { get; set; }
        public string? mobile { get; set; }
        public string? department { get; set; }
        public string? job_title { get; set; }
        public bool is_active { get; set; }
        public string? role { get; set; }
        public string? password { get; set; }

        // القسم اللي الموظف مسؤول عنه. فاضي = بلا تقييد (والدور اللي فيه
        // صلاحية students.allGenders بيتخطّى التقييد أصلًا).
        public Gender? scope_gender { get; set; }
    }
}
