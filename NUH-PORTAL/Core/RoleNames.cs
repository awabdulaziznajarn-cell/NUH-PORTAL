namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  أسماء الأدوار — التعريف الوحيد في النظام.
    //
    //  ⚠️ العلّة التي عالجها هذا الملف: أسماء الأدوار كانت نصوصًا مكتوبة بالإيد
    //     في كل موضع تُستعمل فيه - في Data/DbSeeder.cs وProgram.cs و
    //     Services/OtpFlowService.cs وServices/AuditLogQueryService.cs وغيرها.
    //     ونصٌّ مكرَّر بلا مرجع لا يُخطئ بصوت: لو كُتب "Supervisor" بحرف كبير
    //     في موضع واحد لَما اشتكى المترجم، ولَظلّ الشرط الذي يعتمد عليه صامتًا
    //     يرجع false إلى الأبد.
    //
    //  ⚠️ و"user" هو دور **الطلاب** لا دور موظّف عامّ - رغم أن اسمه يوحي بغير
    //     ذلك. الحساب يُنشأ به لحظة تحقّق الطالب برمز جواله في OtpFlowService.
    //     الاسم لا يُغيَّر هنا لأنه مخزَّن في قاعدة البيانات فعلًا لآلاف
    //     الحسابات، لكن التسمية Student تقول ما يعنيه في كل موضع يُقرأ فيه.
    // ============================================================================
    public static class RoleNames
    {
        public const string Admin      = "admin";
        public const string Supervisor = "supervisor";
        public const string Cyber      = "cyber";

        // دور الطلاب. الاسم في قاعدة البيانات "user" - انظر التحذير أعلاه.
        public const string Student    = "user";

        // أدوار الموظفين: من يعمل على النظام، لا من يُقدّم إليه طلبًا.
        public static readonly string[] Staff = { Admin, Supervisor, Cyber };

        public static bool IsStudent(string? role) =>
            string.Equals(role, Student, System.StringComparison.OrdinalIgnoreCase);
    }
}
