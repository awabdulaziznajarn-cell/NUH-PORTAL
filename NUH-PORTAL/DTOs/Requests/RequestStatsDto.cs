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

        // ⚠️ «بيانات ناقصة» - الطلب المُعاد إلى الطالب لاستكمال بياناته. لم يكن
        //    له عدّاد ولا تبويب، فلا يظهر إلا تحت «الكل»: المراجع يطلب معلومات
        //    ثم يفقد أثر الطلب. وهو الحالة الوحيدة التي كانت تخفي عملًا حقيقيًّا،
        //    بينما التبويبان المحذوفان (موافقة الإسكان/معتمد) لا يقف فيهما طلب.
        public int NeedMoreInfo { get; set; }
    }
}
