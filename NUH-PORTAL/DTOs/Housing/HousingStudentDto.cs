using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Housing
{
    public class HousingStudentDto
    {
        public int Id { get; set; }
        public string? student_id { get; set; }
        public string? full_name { get; set; }
        public string? full_name_english { get; set; }
        public string? college { get; set; }
        public string? department { get; set; }
        public Gender? gender { get; set; }
        public string? academic_level { get; set; }
        public string? phone { get; set; }
        public string? housing_building { get; set; }
        public string? room_number { get; set; }
        public string? apartment_number { get; set; }
        public string? ad_username { get; set; }
        public AdStatus? ad_status { get; set; }
        public DateTime? ad_last_sync_at { get; set; }
        public StudentState? status { get; set; }
        public NUH_PORTAL.Models.Enums.StudentStatus? student_status { get; set; }
    }
}
