namespace NUH_PORTAL.Core
{
    // ========================================================================
    //  قيم Action وSource في سجل حركة التسكين - المصدر الوحيد لها.
    //
    //  ⚠️ ثوابت لا نصوص حرّة: القيمة تُكتب في الخدمة وتُقرأ في الواجهة وتُصفّى
    //     في التقرير. ونصّ حرّ في ثلاثة مواضع يعني أن حرفًا واحدًا مختلفًا
    //     يُسقط الصفّ من التقرير بلا أي رسالة خطأ.
    //
    //  ⚠️ والنصوص المعروضة ليست هنا: هذه رموز تُخزَّن في قاعدة البيانات،
    //     وترجمتها في Resources/SharedResource بالمفتاح hh_action_<القيمة>
    //     وhh_source_<القيمة> - في الملفَّين العربي والإنجليزي معًا.
    // ========================================================================
    public static class HousingHistoryKinds
    {
        public static class Actions
        {
            // تسكين أول: لم يكن للطالب سكن قبله.
            public const string Assigned = "assigned";

            // نقل: من موضع إلى موضع، بقرار من المشرف.
            public const string Transferred = "transferred";

            // تصحيح: تغيير موضع لم يأتِ من شاشة النقل - تصويب إدخال غالبًا.
            public const string Edited = "edited";

            // إخلاء: أُفرغ سكنه ولم يُسكَّن في غيره.
            public const string Cleared = "cleared";
        }

        public static class Sources
        {
            public const string Approval = "approval";        // اعتماد إدارة الإسكان للطلب
            public const string Registration = "registration";// بيانات التسجيل
            public const string StudentsScreen = "students";  // تسجيل أو تعديل طالب
            public const string BulkImport = "bulk";          // الرفع الجماعي
            public const string Transfer = "transfer";        // شاشة نقل السكن
            public const string StatusChange = "status";      // تغيير حالة الطالب
            public const string Backfill = "backfill";        // التعبئة الأوّلية عند إنشاء الجدول
        }
    }
}
