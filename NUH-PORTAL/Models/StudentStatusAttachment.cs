namespace NUH_PORTAL.Models
{
    public class StudentStatusAttachment
    {
        public int Id { get; set; }
        public int StudentStatusActionId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }

        public StudentStatusAction? StudentStatusAction { get; set; }
        public User? UploadedByUser { get; set; }
    }
}
