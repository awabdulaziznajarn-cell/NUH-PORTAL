namespace NUH_PORTAL.Models
{
    public class WorkflowHistory
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string? FromStage { get; set; }
        public string ToStage { get; set; } = string.Empty;
        public int ActionBy { get; set; }
        public DateTime ActionDate { get; set; }
        public string? Notes { get; set; }

        // التعديلات التي أجراها الطالب في هذه الخطوة، بصيغة JSON منظّمة:
        // [{ "field":"housing_building", "label":"رقم المبنى", "old":"40", "new":"43" }]
        // تُستخدم لتعليم الحقول المتغيّرة في شاشة تفاصيل الطلب.
        public string? ChangesJson { get; set; }

        public Request? Request { get; set; }
        public User? Actor { get; set; }
    }
}
