namespace NUH_PORTAL.DTOs.Students
{
    // نفس مفاتيح الإحصائيات القديمة بالحرف (عشان متكسرش لوحة التحكم في الواجهة)
    public class StudentStatsDto
    {
        public int total { get; set; }
        // طلاب/طالبات — مجموعهم = total بالظبط (كل غير المحذوفين)، عشان
        // الرقمين يقروا مع «إجمالي الطلاب» من غير ما حد يحسب الفرق.
        public int male { get; set; }
        public int female { get; set; }
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
        // ⚠️ العدّاد ده كان **مش موجود أصلًا**: الـ DTO فيه housing_rejected و
        //    cyber_rejected بس، والحالة «rejected» المجرّدة — وهي حالة رفض طلب
        //    التسجيل الذاتي للطالب — مكانتش بتتعدّ في أي خانة. يعني طلب مرفوض
        //    من مسار الطالب مش موجود في أي رقم على لوحة التحكم.
        public int rejected { get; set; }
    }
}
