namespace NUH_PORTAL.Models.Enums
{
    // الحالة الدراسية/السكنية للطالب — بتتخزّن كنص زي ما هي (بدون تغيير عمود).
    // left_housing قيمة موجودة فعليًا (بتتسجّل من StudentStatusService لما الطالب يترك الإسكان)،
    // وإن كانت لسه مالهاش مفتاح ترجمة stu_status_* — بنحافظ على السلوك زي ما هو.
    public enum StudentStatus
    {
        active,
        dismissed,
        graduated,
        transferred,
        left_housing
    }
}
