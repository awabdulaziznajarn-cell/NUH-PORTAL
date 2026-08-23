using System.Text.Json;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  مجموعات إجراءات السجل — أي إجراء يخصّ الطلاب، وأيّها يخصّ الطلبات... إلخ.
    //  ده التعريف الوحيد في النظام.
    //
    //  ⚠️ العيب اللي عالجه الملف ده:
    //     المجموعات كانت مكتوبة جوّه AuditLogQueryService كمصفوفات خاصّة،
    //     ومستعملة في تسع مواضع في نفس الملف. تمانية منهم بيقولوا
    //     StudentActions.Contains(...) — والتاسع، اللي بيبني رسم «توزيع عمليات
    //     الطلاب»، كان بيكتب الشرط بالإيد:
    //
    //         a.action == "create_student" || a.action == "update_student"
    //                                      || a.action == "delete_student"
    //
    //     وناسي "checkout_student". يعني كل عمليات إخلاء الطلاب للسكن كانت
    //     **مش موجودة في الرسم** — والرسم مالوش أي علامة إنه ناقص، فالمشرف
    //     بيقرا التوزيع ويفتكره كامل. وأسوأ حاجة في العدّاد الناقص إنه ما بيبانش
    //     غلط: بيبان رقمًا أصغر وبس.
    //
    //  ⚠️ والمصفوفات هنا لا في الخدمة عشان الجافاسكريبت يقراها كمان: شاشة
    //     التقارير محتاجة تعرف «هل الإجراء ده يخصّ الطلاب؟» عشان تحسب أكثر
    //     الموظفين نشاطًا على سجلات الطلاب. لو اتكتبت القايمة في الجافاسكريبت
    //     كانت هتبقى نسخة عاشرة بتفترق أول ما يتضاف إجراء جديد.
    //     التخطيط بيحقنها بـ ToJson() زي جدول مسار الطلب بالظبط.
    // ============================================================================
    public static class AuditActionGroups
    {
        public static readonly string[] Student =
        {
            "create_student", "update_student", "delete_student", "checkout_student"
        };

        public static readonly string[] Request =
        {
            "create_request", "approve_request", "reject_request",
            "housing_approve_request", "housing_reject_request",
            "submit_cyber_review",
            "cyber_approve_request", "cyber_reject_request"
        };

        public static readonly string[] Login =
        {
            "login", "login_failed", "logout",
            "login_admin_fallback", "login_admin_fallback_failed"
        };

        // ⚠️ سكن أعضاء هيئة التدريس: بياناته تُكتب في الدليل النشط، وكان سجله
        //    بلا فلتر - فالبحث عن «من عدّل هذه الوحدة» يعني تصفّح السجل كله
        //    صفحةً صفحة. الإجراءات هنا هي التي تكتبها FacultyHousingService.
        public static readonly string[] Faculty =
        {
            "faculty_occupant_updated", "faculty_handover",
            "faculty_service_started", "faculty_service_stopped",
            "faculty_ad_push", "faculty_ad_push_failed", "faculty_import_applied",
            // ⚠️ نقل الحساب بين OU البنين والبنات. أي إجراء جديد في
            //    FacultyHousingService لازم يتضاف هنا في نفس اللحظة، وإلا
            //    بيتكتب في قاعدة البيانات وما بيظهرش في فلتر «سكن أعضاء هيئة
            //    التدريس» - يعني السجل كامل والتقرير ناقص، وde أخطر من إن
            //    الإجراء ما اتسجّلش أصلًا لأن التقرير بيبان مكتمل.
            "faculty_ad_ou_move",
            // اكتشاف إن مسار الحساب في الدليل اتغيّر من بره النظام
            "faculty_ad_dn_drift"
        };

        public static readonly string[] Password = { "set_password" };
        public static readonly string[] Logout   = { "logout" };
        public static readonly string[] Ad       = { "user_created_ad", "user_updated_ad" };

        // ⚠️ مفاتيح الفلتر في الواجهة (actionGroup=) بتترجم هنا وبس. كانت
        //    switch جوّه ApplyFilters، فأي مجموعة جديدة كان لازم تتضاف في
        //    مكانين: تعريف المصفوفة والـ switch.
        public static string[] ByKey(string? key) => (key ?? "").Trim().ToLowerInvariant() switch
        {
            "login"    => Login,
            "student"  => Student,
            "request"  => Request,
            "password" => Password,
            "logout"   => Logout,
            "ad"       => Ad,
            "faculty"  => Faculty,
            _ => Array.Empty<string>()
        };

        // بتتحقن في التخطيط كـ window.__AUDIT_GROUPS — الواجهة بتقرا منها
        // ولا تكتب أي قائمة إجراءات بنفسها.
        public static string ToJson() => JsonSerializer.Serialize(new
        {
            student = Student,
            request = Request,
            login = Login,
            faculty = Faculty
        });
    }
}
