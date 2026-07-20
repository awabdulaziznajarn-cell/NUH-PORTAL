using System.Text.Json.Serialization;

namespace NUH_PORTAL.Models
{
    public class BulkRequestStudent
    {
        public int Id { get; set; }
        public int BulkRequestId { get; set; }
        public string StudentID { get; set; } = string.Empty;
        public string NationalID { get; set; } = string.Empty;
        public string FullNameArabic { get; set; } = string.Empty;
        public string FullNameEnglish { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string? College { get; set; }
        public string? Department { get; set; }
        public string? AcademicLevel { get; set; }
        public string? Gender { get; set; }
        public string? BuildingNumber { get; set; }
        public string? ApartmentNumber { get; set; }
        public string? RoomNumber { get; set; }
        public bool IsValid { get; set; } = true;
        public string? ErrorMessage { get; set; }

        [JsonIgnore]
        public BulkRequest? BulkRequest { get; set; }
    }
}
