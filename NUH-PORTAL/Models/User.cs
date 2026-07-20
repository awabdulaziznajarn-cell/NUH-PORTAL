namespace NUH_PORTAL.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? username { get; set; }
        public string? full_name { get; set; }
        public string? email { get; set; }
        public string? role { get; set; }
        public DateTime created_at { get; set; }
        public bool is_active { get; set; }
        public string? password_hash { get; set; }
        public string? department { get; set; }
        public string? mobile { get; set; }
        public string? job_title { get; set; }
    }
}
