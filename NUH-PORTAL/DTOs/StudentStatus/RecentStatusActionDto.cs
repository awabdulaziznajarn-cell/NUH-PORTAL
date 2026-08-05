namespace NUH_PORTAL.DTOs.StudentStatus
{
    public class RecentStatusActionDto
    {
        public int Id { get; set; }
        public string? StudentNumber { get; set; }
        public string? StatusType { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CreatedBy { get; set; }
        public string? StudentName { get; set; }
        // اسم من نفّذ الإجراء — من السيرفر مباشرة. الواجهة كانت بتجيبه من /api/users
        // وde مقفول على المشرف، فكان بيطلع "غير معروف" دايمًا عنده.
        public string? CreatedByName { get; set; }
        // اسم الملف المرفق بالإجراء (لو فيه) — كان بيظهر في شاشة المغادرة بس.
        public string? AttachmentFileName { get; set; }
    }
}
