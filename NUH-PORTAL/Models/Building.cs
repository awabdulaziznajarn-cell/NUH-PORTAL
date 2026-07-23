using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Models
{
    // قائمة المباني السكنية (lookup مُدار)
    public class Building
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // رقم المبنى: 65, 66...
        public string ArName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        // نوع المبنى: بنين (Male) أو بنات (Female) — نفس enum جنس الطالب. بيترشّح عليه المبنى وقت التسجيل.
        public Gender? Gender { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
