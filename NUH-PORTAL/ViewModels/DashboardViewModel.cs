using NUH_PORTAL.DTOs.Requests;
using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.DTOs.StudentStatus;

namespace NUH_PORTAL.ViewModels
{
    // موديل لوحة التحكم — الأرقام والقوائم اللي بتترندر من السيرفر مباشرة،
    // الباقي (رسوم/سجل اليوم/تنبيهات) بيتحمّل بالـ JS من نفس الـ APIs القديمة
    public class DashboardViewModel
    {
        public StudentStatsDto Stats { get; set; } = new();
        public StudentStatusStatsDto StatusStats { get; set; } = new();
        public List<StudentDto> LatestStudents { get; set; } = new();
        public List<RequestDto> LatestRequests { get; set; } = new();
    }
}
