using System.Text.Json;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  مجموعات إجراءات السجل - أي إجراء يخصّ الطلاب، وأيّها يخصّ الطلبات... إلخ.
    //  ده التعريف الوحيد في النظام.
    //
    //  ⚠️ العيب اللي عالجه الملف ده:
    //     المجموعات كانت مكتوبة جوّه AuditLogQueryService كمصفوفات خاصّة،
    //     ومستعملة في تسع مواضع في نفس الملف. تمانية منهم بيقولوا
    //     StudentActions.Contains(...) - والتاسع، اللي بيبني رسم «توزيع عمليات
    //     الطلاب»، كان بيكتب الشرط بالإيد:
    //
    //         a.action == "create_student" || a.action == "update_student"
    //                                      || a.action == "delete_student"
    //
    //     وناسي "checkout_student". يعني كل عمليات إخلاء الطلاب للسكن كانت
    //     **مش موجودة في الرسم** - والرسم مالوش أي علامة إنه ناقص، فالمشرف
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
            "cyber_approve_request", "cyber_reject_request",
            // ⚠️ طباعة وثيقة التعهّد. الورقة دي بتتطبع وقت التحقيق وبتدخل
            //    المحضر، فسؤال «مين طبع النسخة دي وامتى؟» بيتسأل فعلًا - وكان
            //    النظام مايعرفش يجاوبه: فتح ملف الطالب متسجّل والطباعة لأ.
            "pledge_printed"
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

        // ====================================================================
        //  الخريطة الوحيدة: مفتاح الفلتر -> إجراءاته. كل ما عداها يقرأ منها.
        //
        //  ⚠️ كانت القائمة مكتوبة في **أربعة** مواضع متفارقة: switch هنا،
        //     ومصفوفة في wwwroot/js/reports-page.js، وأخرى داخل <script> في
        //     Views/AuditLog/Index.cshtml، ونسخة ثالثة أضيق في ToJson. والفروق
        //     لم تكن نظرية: «سكن أعضاء هيئة التدريس» كان غائبًا عن شاشة
        //     التقارير كلّها رغم أن الخادم يدعمه، و«الترتيب» في الشاشتين مختلف،
        //     وToJson تُسقط ثلاث مجموعات فتعود فارغة لمن يسأل عنها.
        //
        //  ⚠️ والترتيب هنا هو ترتيب العرض في الفلتر - القاموس مرتَّب لا مبعثر:
        //     الأكثر استعمالًا أولًا، والمجموعات الفنية في آخره.
        // ====================================================================
        private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["student"]  = Student,
            ["request"]  = Request,
            ["login"]    = Login,
            ["logout"]   = Logout,
            ["faculty"]  = Faculty,
            ["ad"]       = Ad,
            ["password"] = Password
        };

        // مفاتيح المجموعات بترتيب العرض. الواجهة تبني الفلتر منها لا من مصفوفة
        // عندها، ونصّ كل مفتاح في ملف الترجمة باسم agrp_<key>.
        public static string[] Keys => Map.Keys.ToArray();

        public static string[] ByKey(string? key) =>
            Map.TryGetValue((key ?? "").Trim(), out var actions) ? actions : Array.Empty<string>();

        // بتتحقن في التخطيط كـ window.__AUDIT_GROUPS - الواجهة بتقرا منها
        // ولا تكتب أي قائمة إجراءات بنفسها.
        //
        // ⚠️ كل المجموعات لا أربعة منها: النسخة القديمة كانت تُسقط password
        //    وlogout وad، فأي شاشة تسأل عن واحدة منها تتلقّى فراغًا وترسم
        //    قائمة خالية بلا شكوى.
        public static string ToJson() => JsonSerializer.Serialize(Map);
    }
}
