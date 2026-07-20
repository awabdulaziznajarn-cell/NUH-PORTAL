namespace NUH_PORTAL.Models
{
    public class HousingTransfer
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string? StudentNumber { get; set; }
        public string? OldBuilding { get; set; }
        public string? OldApartment { get; set; }
        public string? OldRoom { get; set; }
        public string? NewBuilding { get; set; }
        public string? NewApartment { get; set; }
        public string? NewRoom { get; set; }
        public string? Reason { get; set; }
        public string? CustomReason { get; set; }
        public string? AttachmentPath { get; set; }
        public string? OriginalFileName { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public Student? Student { get; set; }
        public User? CreatedByUser { get; set; }
    }
}
