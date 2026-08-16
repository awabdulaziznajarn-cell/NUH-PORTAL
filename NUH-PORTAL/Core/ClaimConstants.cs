namespace NUH_PORTAL.Core
{
    // أنواع الـ claims المخصّصة. الصلاحيات بتتخزّن كـ claims على الدور، وبتتحمّل في التوكن/الكوكي وقت الدخول.
    public static class ClaimConstants
    {
        public const string Permission = "permission";

        // القسم اللي الموظف مسؤول عنه (male/female). بيتحمّل من قاعدة البيانات مع
        // كل طلب زي الصلاحيات بالظبط — مش وقت الدخول — عشان تغيير القسم من شاشة
        // المستخدمين يسري من غير ما الموظف يخرج ويدخل تاني.
        public const string ScopeGender = "scope_gender";
    }
}
