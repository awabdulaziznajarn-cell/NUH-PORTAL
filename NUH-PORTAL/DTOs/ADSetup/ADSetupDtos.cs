using NUH_PORTAL.Services;

namespace NUH_PORTAL.DTOs.ADSetup
{
    public class ADReadinessReport
    {
        public DateTime Timestamp { get; set; }
        public AdHealthResult ADConnectivity { get; set; } = new();
        public ADServiceAccountStatus? ServiceAccount { get; set; }
        public ADObjectValidation[]? OUs { get; set; }
        public ADObjectValidation[]? Groups { get; set; }
        public ADPasswordPolicyResult? PasswordPolicy { get; set; }
    }

    public class ADObjectValidation
    {
        public string Dn { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public string? Error { get; set; }
    }

    public class ADDryRunResult
    {
        public object? Student { get; set; }
        public object? ProposedAccount { get; set; }
        public object? TempPassword { get; set; }
        public string? LdapPath { get; set; }
        public object? PasswordPolicyCheck { get; set; }
    }

    // ========================================================================
    //  طلب إنشاء حساب اختباري في الدليل.
    //
    //  ⚠️ كان فيه TargetOu و TargetGroup بيتاخدوا من العميل زي ما هما. الخدمة
    //     كانت بتبني CN={الرقم},{TargetOu} وتعمل الحساب **وتفعّله** وتضيفه
    //     للمجموعة اللي العميل بعتها. يعني صاحب صلاحية system.adSetup يبعت
    //     "CN=Domain Admins,CN=Users,DC=..." فيطلع حساب مفعّل في مجموعة مديري
    //     الدومين - تصعيد من أدمن بوابة لأدمن دومين، محدود بس بصلاحيات حساب
    //     الخدمة في الدليل.
    //
    //  ⚠️ والحقلان اتشالوا خالص لا اتتجاهلوا: حقل بيتبعت وبيتتجاهل بيوهم اللي
    //     بيقرا الـ API إنه شغّال. المسارات دلوقتي بتتقرا من الإعدادات
    //     (Services/AdDirectoryLayout) زي مسار الإنشاء الحقيقي بالظبط، واللي
    //     العميل بيقدر يختاره هو **القسم** وبس.
    // ========================================================================
    public class ADTestUserRequest
    {
        public string StudentId { get; set; } = string.Empty;

        // اختياري - الافتراضي قسم الطلاب. القيمة بتتحوّل لمسار من الإعدادات،
        // ومابتوصلش للدليل كنصّ.
        public NUH_PORTAL.Models.Enums.Gender? Gender { get; set; }
    }

    public class ADTestUserReport
    {
        public string RequestedSamAccountName { get; set; } = string.Empty;
        public string TargetOu { get; set; } = string.Empty;
        public string TargetGroup { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool OverallSuccess { get; set; }
        public string? Summary { get; set; }
        public List<ADTestStep> Steps { get; set; } = new();
        public ADReadUserResult? ReadUserResult { get; set; }
    }

    public class ADTestStep
    {
        public string Step { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Running";
        public string? Error { get; set; }
        public string? Details { get; set; }
    }
}
