namespace NUH_PORTAL.Models
{
    public class OTPVerification
    {
        public int Id { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public string OTPHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public int Attempts { get; set; }
        public string? IPAddress { get; set; }
    }
}
