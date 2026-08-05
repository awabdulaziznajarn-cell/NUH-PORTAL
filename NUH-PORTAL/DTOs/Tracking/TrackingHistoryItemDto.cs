namespace NUH_PORTAL.DTOs.Tracking
{
    public class TrackingHistoryItemDto
    {
        public string? ToStage { get; set; }
        public DateTime ActionDate { get; set; }
        public string? Notes { get; set; }
        // ⚠️ ما فيش ActorName هنا عن قصد. /api/RequestTracking مفتوح بدون
        //    مصادقة، فكان بيسرّب أسماء الموظفين (بما فيها الحسابات الإدارية)
        //    لأي حد معاه رقم طلب. الطالب يهمّه الجهة والتاريخ، مش اسم الموظف.
    }
}
