namespace NUH_PORTAL.Core
{
    // أنواع الـ claims المخصّصة. الصلاحيات بتتخزّن كـ claims على الدور، وبتتحمّل في التوكن/الكوكي وقت الدخول.
    public static class ClaimConstants
    {
        public const string Permission = "permission";
    }
}
