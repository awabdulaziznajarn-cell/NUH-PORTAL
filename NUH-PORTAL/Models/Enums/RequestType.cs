namespace NUH_PORTAL.Models.Enums
{
    // نوع الطلب. الأسماء snake_case عمداً عشان تطابق القيم النصية المخزّنة/المتوقّعة في الـ JSON بالظبط.
    // "housing" قيمة قديمة موجودة في بيانات سابقة — مضمّنة عشان قراءتها ماتكسرش.
    public enum RequestType
    {
        self_registration,
        bulk_req,
        housing
    }
}
