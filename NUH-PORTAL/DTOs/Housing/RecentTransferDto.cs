namespace NUH_PORTAL.DTOs.Housing
{
    public class RecentTransferDto
    {
        public int Id { get; set; }
        public string? StudentNumber { get; set; }
        public string? OldBuilding { get; set; }
        public string? OldFloor { get; set; }
        public string? OldApartment { get; set; }
        public string? OldRoom { get; set; }
        public string? NewBuilding { get; set; }
        public string? NewFloor { get; set; }
        public string? NewApartment { get; set; }
        public string? NewRoom { get; set; }
        public string? Reason { get; set; }
        public string? CustomReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string? AttachmentPath { get; set; }
        public string? OriginalFileName { get; set; }
        public string? StudentName { get; set; }
        // اسم من نفّذ النقل — بيرجع من السيرفر بدل ما الواجهة تجيبه من /api/users،
        // لأن المشرف مالوش صلاحية users.view فالنداء ده بيفشل عنده والاسم كان بيطلع "غير معروف".
        public string? CreatedByName { get; set; }
    }
}
