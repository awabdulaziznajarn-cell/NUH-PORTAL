namespace NUH_PORTAL.Models.Enums
{
    // جنس الطالب/المبنى — enum بدل الـ magic strings.
    // بيتخزّن في الـ DB كـ "male"/"female" (ValueConverter) وبيتسلسل في الـ JSON كـ "male"/"female" (JsonConverter)،
    // فمفيش ترحيل بيانات ولا تغيير في الـ JS.
    public enum Gender
    {
        Male = 1,
        Female = 2
    }
}
