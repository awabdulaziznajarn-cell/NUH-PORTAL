namespace NUH_PORTAL.DTOs.Students
{
    // عدد الساكنين في مبنى واحد — تغذّي رسم «الطلاب حسب المبنى» في الصفحة الرئيسية
    public class BuildingCountDto
    {
        // كود المبنى كما هو مخزَّن ("41") — الواجهة تحوّله لاسم عبر القوائم المرجعية
        public string Building { get; set; } = "";
        public int Count { get; set; }
    }
}
