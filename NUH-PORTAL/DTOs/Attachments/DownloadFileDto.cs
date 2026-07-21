namespace NUH_PORTAL.DTOs.Attachments
{
    // بيانات ملف جاهز للتحميل (المسار الفعلي بيتحول لـ PhysicalFile في الكنترولر)
    public class DownloadFileDto
    {
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
    }
}
