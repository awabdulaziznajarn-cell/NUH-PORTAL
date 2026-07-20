namespace NUH_PORTAL.DTOs.Students
{
    // نفس مفاتيح الإحصائيات القديمة بالحرف (عشان متكسرش لوحة التحكم في الواجهة)
    public class StudentStatsDto
    {
        public int total { get; set; }
        public int active { get; set; }
        public int left { get; set; }
        public int submitted { get; set; }
        public int housing_approved { get; set; }
        public int housing_rejected { get; set; }
        public int cyber_review { get; set; }
        public int cyber_approved { get; set; }
        public int cyber_rejected { get; set; }
        public int ready_for_provisioning { get; set; }
        public int completed { get; set; }
    }
}
