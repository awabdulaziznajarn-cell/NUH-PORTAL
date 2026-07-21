namespace NUH_PORTAL.DTOs.Common
{
    // نتيجة ملف جاهز للتنزيل (Excel/غيره) بيتحول لـ File() في الكنترولر
    public class FileResultDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
