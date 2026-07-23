namespace NUH_PORTAL.Models.Enums
{
    // حالة سجل الطالب: نشط / غير نشط / غادر — بتتخزّن كنص زي ما هي (بدون تغيير عمود).
    // ملحوظة: ده حقل status وهو غير student_status (الحالة الدراسية) — حقلين مختلفين على نفس الكيان.
    public enum StudentState
    {
        active,
        inactive,
        left
    }
}
