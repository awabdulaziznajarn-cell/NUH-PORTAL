namespace NUH_PORTAL.Models
{
    // سجل الدخول والخروج — بيسجّل كل محاولة (نجاح/فشل) مع اسم المستخدم المُدخَل حتى لو فشل.
    // event_type ∈ login_success | login_failed | logout ، method ∈ ad | local | session
    public class SignInLog
    {
        public int Id { get; set; }
        public DateTime occurred_at { get; set; }
        public int? user_id { get; set; }
        public string? username { get; set; }
        public string event_type { get; set; } = "login_success";
        public string? method { get; set; }
        public bool success { get; set; }
        public string? detail { get; set; }
        public string? ip_address { get; set; }
        public string? user_agent { get; set; }

        public User? User { get; set; }
    }
}
