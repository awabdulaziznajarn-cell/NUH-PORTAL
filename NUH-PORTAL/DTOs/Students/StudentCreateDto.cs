using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Students
{
    // مدخلات إنشاء طالب (الحقول اللي الواجهة بتبعتها ويتم التحقق منها/تطبيقها)
    public class StudentCreateDto
    {
        public string? student_id { get; set; }
        public string? full_name { get; set; }
        public string? full_name_english { get; set; }
        public string? national_id { get; set; }
        public string? phone { get; set; }
        public Gender? gender { get; set; }
        public string? college { get; set; }
        public string? department { get; set; }
        public string? academic_level { get; set; }
        public string? housing_building { get; set; }
        public string? room_number { get; set; }
        public string? apartment_number { get; set; }
        public StudentState? status { get; set; }
        public NUH_PORTAL.Models.Enums.StudentStatus? student_status { get; set; }
    }
}
