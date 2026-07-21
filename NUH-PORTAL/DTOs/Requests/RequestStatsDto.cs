namespace NUH_PORTAL.DTOs.Requests
{
    // عدادات الطلبات حسب الحالة — لصفحة إدارة الطلبات (الحساب بيتم في السيرفر بدل ما كان بيتعد في المتصفح)
    public class RequestStatsDto
    {
        public int Total { get; set; }
        public int Submitted { get; set; }
        public int HousingApproved { get; set; }
        public int CyberReview { get; set; }
        public int CyberApproved { get; set; }
        public int ReadyForProvisioning { get; set; }
        public int Completed { get; set; }
        public int Rejected { get; set; }
    }
}
