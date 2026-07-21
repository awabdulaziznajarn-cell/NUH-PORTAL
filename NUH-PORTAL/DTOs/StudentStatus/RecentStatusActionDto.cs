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
    }
}
