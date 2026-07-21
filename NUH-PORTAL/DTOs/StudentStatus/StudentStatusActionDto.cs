namespace NUH_PORTAL.DTOs.StudentStatus
{
    // سجل إجراء حالة (نفس حقول الـ entity من غير الـ navigations)
    public class StudentStatusActionDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string? StudentNumber { get; set; }
        public string? StatusType { get; set; }
        public string? Notes { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool PendingADAction { get; set; }
        public bool? ADActionCompleted { get; set; }
        public DateTime? ADActionDate { get; set; }
    }
}
