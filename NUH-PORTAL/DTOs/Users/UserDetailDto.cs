using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Users
{
    // بيانات مستخدم كاملة لفورم التعديل (من غير الباسورد طبعًا).
    public class UserDetailDto
    {
        public int Id { get; set; }
        public string? username { get; set; }
        public string? full_name { get; set; }
        public string? email { get; set; }
        public string? mobile { get; set; }
        public string? department { get; set; }
        public string? job_title { get; set; }
        public string? role { get; set; }
        public bool is_active { get; set; }
        public bool is_deleted { get; set; }
        public string? auth_source { get; set; }
        public Gender? scope_gender { get; set; }
        public DateTime created_at { get; set; }
    }
}
