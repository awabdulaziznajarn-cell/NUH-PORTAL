namespace NUH_PORTAL.DTOs.Users
{
    // عدّادات تبويبات شاشة المستخدمين — الموظفون مقابل حسابات دخول الطلاب.
    public class UserCountsDto
    {
        public int Staff { get; set; }
        public int Students { get; set; }
        // تبويب ثالث: الحسابات المحذوفة (حذف منطقي) — منها بتتعمل الاستعادة
        public int Deleted { get; set; }
    }
}
