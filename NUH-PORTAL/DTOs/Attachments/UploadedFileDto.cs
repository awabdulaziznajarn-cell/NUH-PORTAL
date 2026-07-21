namespace NUH_PORTAL.DTOs.Attachments
{
    public class UploadedFileDto
    {
        public int Id { get; set; }
        public string? OriginalFileName { get; set; }
        public long FileSize { get; set; }
        public string? ContentType { get; set; }
        public string? DocumentType { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
