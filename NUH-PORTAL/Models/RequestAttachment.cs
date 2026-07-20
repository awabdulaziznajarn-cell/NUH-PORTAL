namespace NUH_PORTAL.Models
{
    public class RequestAttachment
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? DocumentType { get; set; }
        public string? Notes { get; set; }
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public bool IsDeleted { get; set; }

        public Request? Request { get; set; }
        public User? UploadedByUser { get; set; }
    }
}
