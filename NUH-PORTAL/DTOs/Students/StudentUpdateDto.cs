using NUH_PORTAL.Models.Enums;

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
        public Gender? gender { get; set; }
        public string? college { get; set; }
        public string? department { get; set; }
        public string? academic_level { get; set; }
        public string? housing_building { get; set; }
        // الدور: "0" = الأرضي، و"1".."4". بيتخزّن كود مش نص معروض عشان الترتيب
        // والفرز يفضلوا رقميين، والعرض بيترجمه (floorName في request-details-page.js).
        public string? floor_number { get; set; }
        public string? room_number { get; set; }
        public string? apartment_number { get; set; }
        public StudentState? status { get; set; }
    }
}
