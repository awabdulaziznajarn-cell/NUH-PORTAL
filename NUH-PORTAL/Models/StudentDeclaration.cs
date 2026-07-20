namespace NUH_PORTAL.Models
{
    public class StudentDeclaration
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public bool DeclarationAccepted { get; set; }
        public bool PolicyAccepted { get; set; }
        public string PolicyVersion { get; set; } = string.Empty;
        public DateTime AcceptedDate { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }

        public Request? Request { get; set; }
    }
}
