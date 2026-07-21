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
        public DateTime created_at { get; set; }
        public bool is_active { get; set; }
    }
}
