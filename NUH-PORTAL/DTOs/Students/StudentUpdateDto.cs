namespace NUH_PORTAL.DTOs.Students
{
    // مدخلات تعديل طالب. ملاحظة: student_id موجود للتحقق فقط (بنفس منطق الكود القديم) ولا يتم تعديله
    public class StudentUpdateDto
    {
        public string? student_id { get; set; }
        public string? full_name { get; set; }
        public string? full_name_english { get; set; }
        public string? national_id { get; set; }
        public string? phone { get; set; }
        public string? gender { get; set; }
        public string? college { get; set; }
        public string? department { get; set; }
        public string? academic_level { get; set; }
        public string? housing_building { get; set; }
        public string? room_number { get; set; }
        public string? apartment_number { get; set; }
        public string? status { get; set; }
    }
}
