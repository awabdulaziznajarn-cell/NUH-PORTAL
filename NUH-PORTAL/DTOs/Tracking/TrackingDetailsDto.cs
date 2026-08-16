namespace NUH_PORTAL.DTOs.Tracking
{
    // تفاصيل التتبع برقم الطلب (نفس شكل الرد القديم)
    public class TrackingDetailsDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? StudentName { get; set; }
        // ⚠️ AdUsername اتشال من هنا. الشاشة دي عامة بلا تسجيل دخول، واسم
        //    حساب الشبكة نص بيانات الدخول — ومشتقّ من الرقم الجامعي كمان
        //    (h + الرقم) فبيكشفه هو التاني. الطالب بياخد بياناته برسالة نصية،
        //    والشاشة بتقوله كده صراحةً، فمفيش سبب تعرضه هنا أصلًا.
        public List<TrackingHistoryItemDto> History { get; set; } = new();
    }
}
