namespace NUH_PORTAL.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string? student_id { get; set; }
        public string? full_name { get; set; }
        public string? national_id { get; set; }
        public string? full_name_english { get; set; }
        public string? academic_level { get; set; }
        public string? phone { get; set; }
        public string? gender { get; set; }
        public string? college { get; set; }
        public string? department { get; set; }
        public string? ad_username { get; set; }
        public string? housing_building { get; set; }
        public string? room_number { get; set; }
        public string? apartment_number { get; set; }
        public string? status { get; set; }
        public string? student_status { get; set; }
        public DateTime created_at { get; set; }
        public int created_by { get; set; }
        public bool IsDeleted { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
        public int? RestoredBy { get; set; }
        public DateTime? RestoredDate { get; set; }

        public string? ad_status { get; set; }
        public DateTime? ad_last_sync_at { get; set; }
        public string? ad_extension_phone { get; set; }
        public string? ad_extension_building { get; set; }
        public string? ad_extension_room { get; set; }
        public string? ad_extension_apartment { get; set; }
        public string? ad_extension_college { get; set; }
        public string? ad_extension_department { get; set; }
        public string? ad_extension_academic_level { get; set; }
    }
}
