namespace NUH_PORTAL.Models.Enums
{
    // حالة حساب الـ AD للطالب — بتتخزّن كنص "enabled"/"disabled" زي ما هي.
    // null = مفيش حساب / غير معروف (مش عضو في الـ enum عشان يفضل nullable على مستوى الحقل).
    public enum AdStatus
    {
        enabled,
        disabled
    }
}
