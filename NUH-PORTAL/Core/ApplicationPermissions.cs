using System.Collections.ObjectModel;

namespace NUH_PORTAL.Core
{
    // كل صلاحيات النظام معرّفة في مكان واحد ومقسّمة لمجموعات (زي الـ permit).
    // كل صلاحية = policy بتتسجّل في Program.cs، والكنترولر بيستخدمها: [Authorize(Policy = "users.manage")].
    //
    // القاعدة: الصلاحية بتوصف *إجراء واحد* المسؤول يفهمه من غير ما يفتح الكود.
    // لو محتاج تشرح صلاحية بجملة فيها "و" كتير، غالبًا لازم تتقسّم.
    public static class ApplicationPermissions
    {
        public const string UsersGroup = "المستخدمون";
        public const string RolesGroup = "الأدوار والصلاحيات";
        public const string LogsGroup = "السجلّات";
        public const string StudentsGroup = "الطلاب";
        public const string RequestsGroup = "الطلبات";
        public const string HousingGroup = "السكن وحسابات الشبكة";
        public const string LookupsGroup = "القوائم المرجعية";
        public const string ReportsGroup = "التقارير";
        public const string SystemGroup = "إعدادات النظام";

        // المستخدمون
        public static readonly ApplicationPermission ViewUsers = new("عرض المستخدمين", "users.view", UsersGroup, "شاشة «المستخدمون»: عرض موظفي النظام وبياناتهم وأدوارهم (قراءة فقط)");
        public static readonly ApplicationPermission ManageUsers = new("إدارة المستخدمين", "users.manage", UsersGroup, "إضافة موظف، تعديل بياناته، تفعيل أو تعطيل حسابه، وضبط كلمة مروره");
        public static readonly ApplicationPermission AddUserFromLdap = new("إضافة من الدليل", "users.addFromLdap", UsersGroup, "زر «إضافة من الدليل»: سحب موظف موجود في Active Directory بدل كتابة بياناته يدويًا");

        // الأدوار والصلاحيات
        public static readonly ApplicationPermission ViewRoles = new("عرض الأدوار", "roles.view", RolesGroup, "شاشة «الأدوار والصلاحيات»: عرض الأدوار وصلاحيات كل دور (قراءة فقط)");
        public static readonly ApplicationPermission ManageRoles = new("إدارة الأدوار", "roles.manage", RolesGroup, "إنشاء دور، تعديل صلاحياته، حذفه - أخطر صلاحية في النظام، إذ تتيح لصاحبها توسيع صلاحيات أي مستخدم");
        public static readonly ApplicationPermission AssignRoles = new("إسناد الأدوار", "roles.assign", RolesGroup, "تغيير دور الموظف من شاشة المستخدمين (مشرف / أمن سيبراني / مدير النظام)");

        // السجلّات
        public static readonly ApplicationPermission ViewAuditLogs = new("سجل الإجراءات", "auditLogs.view", LogsGroup, "شاشة «سجل الإجراءات»: من نفّذ الإجراء ومتى - تسجيل، نقل، اعتماد، رفض، تغيير حالة");
        public static readonly ApplicationPermission ViewAllAuditLogs = new("سجل الإجراءات - كل الإدارات", "auditLogs.viewAll", LogsGroup, "بلا هذه الصلاحية يرى الموظف إجراءات إدارته وإجراءات الطلاب فقط، ولا يرى إجراءات موظفي الإدارات الأخرى. امنحها لمن يحتاج صورة النظام كاملة");
        public static readonly ApplicationPermission ViewErrorLogs = new("سجل الأخطاء", "errorLogs.view", LogsGroup, "شاشة «سجل الأخطاء»: أخطاء النظام التقنية - للدعم الفني");
        public static readonly ApplicationPermission ViewSignInLog = new("سجل الدخول والخروج", "signInLog.view", LogsGroup, "شاشة «سجل الدخول»: محاولات الدخول الناجحة والفاشلة ووقتها وجهازها");

        // الطلاب
        public static readonly ApplicationPermission ViewStudents = new("عرض الطلاب", "students.view", StudentsGroup, "شاشة «الطلاب»: البحث والاطلاع على بيانات الطالب وسكنه وحالته (قراءة فقط)");
        public static readonly ApplicationPermission CreateStudents = new("تسجيل طالب", "students.create", StudentsGroup, "شاشة «تسجيل طالب»: إدخال طالب واحد يدويًا بكامل بياناته وسكنه");
        public static readonly ApplicationPermission BulkImportStudents = new("الرفع الجماعي (إكسل)", "students.bulkImport", StudentsGroup, "شاشة «الرفع الجماعي»: تنزيل القالب ورفع ملف إكسل بعشرات الطلاب مرة واحدة");
        public static readonly ApplicationPermission EditStudents = new("تعديل بيانات طالب", "students.edit", StudentsGroup, "تعديل بيانات طالب مسجّل (الاسم، الجوال، الكلية، السكن) من شاشة الطلاب");
        public static readonly ApplicationPermission DeleteStudents = new("حذف واستعادة طالب", "students.delete", StudentsGroup, "حذف سجل طالب من النظام أو استعادته بعد الحذف - إجراء حسّاس، يُفضّل قصره على مدير النظام");
        public static readonly ApplicationPermission ChangeStudentStatus = new("تحديث حالة الطالب", "students.changeStatus", StudentsGroup, "شاشة «تحديث حالة الطالب»: تسجيل تخرّج / فصل / تحويل / ترك السكن. تنبيه: التخرّج والفصل والتحويل تُعطِّل حساب الشبكة تلقائيًا");
        public static readonly ApplicationPermission OverrideStudentStatus = new("تصحيح حالة نهائية", "students.overrideStatus", StudentsGroup, "تسجيل حالة نهائية جديدة لطالب حالته النهائية مسجّلة بالفعل - لتصحيح إدخال خاطئ فقط، يُفضّل قصره على مدير النظام");

        // الطلبات
        public static readonly ApplicationPermission ViewRequests = new("عرض الطلبات", "requests.view", RequestsGroup, "شاشة «الطلبات»: عرض الطلبات وتفاصيلها ومسارها - يرى كل مستخدم الطلبات الواقعة في مرحلته وفق صلاحيات المراجعة");
        public static readonly ApplicationPermission CreateRequests = new("تقديم طلب نيابة عن طالب", "requests.create", RequestsGroup, "إنشاء طلب سكن لطالب من داخل النظام بدلًا من أن يقدّمه الطالب بنفسه");
        public static readonly ApplicationPermission RequestAttachments = new("مرفقات الطلب", "requests.attachments", RequestsGroup, "عرض وتحميل ورفع وحذف مرفقات الطلب (صور الهوية والوثائق) - بيانات شخصية حسّاسة");
        public static readonly ApplicationPermission ReviewHousing = new("مراجعة إدارة الإسكان", "requests.reviewHousing", RequestsGroup, "المرحلة الأولى: اعتماد أو رفض أو طلب معلومات إضافية على الطلب وهو عند إدارة الإسكان");
        public static readonly ApplicationPermission ReviewCyber = new("مراجعة الأمن السيبراني", "requests.reviewCyber", RequestsGroup, "المرحلة الثانية: اعتماد أو رفض الطلب بعد موافقة إدارة الإسكان عليه وإحالته إلى الأمن السيبراني");
        public static readonly ApplicationPermission CompleteRequests = new("إكمال الطلب وإنشاء الحساب", "requests.complete", RequestsGroup, "المرحلة الأخيرة: إغلاق الطلب وإنشاء حساب الشبكة للطالب فعليًا في Active Directory");

        // السكن وحسابات الشبكة
        public static readonly ApplicationPermission ViewHousing = new("عرض السكن والحسابات", "housing.view", HousingGroup, "شاشة «السكن»: عرض المباني والوحدات وحسابات الشبكة وحالتها (قراءة فقط)");
        public static readonly ApplicationPermission TransferHousing = new("نقل سكن طالب", "housing.transfer", HousingGroup, "نقل طالب من مبنى أو دور أو شقة أو غرفة إلى أخرى مع إرفاق مستند النقل");
        public static readonly ApplicationPermission ManageHousingAccounts = new("إدارة حسابات الشبكة", "housing.manageAccounts", HousingGroup, "تفعيل أو تعطيل حساب الطالب في الشبكة، تصفير كلمة مروره، وإعادة إنشاء الحساب");
        public static readonly ApplicationPermission SyncAd = new("مزامنة وإعدادات الأكتف دايركتوري", "housing.syncAd", HousingGroup, "زر «مزامنة حسابات الشبكة» وتعديل إعدادات الاتصال بالأكتف دايركتوري (مسارات الـ OU والمجموعات)");

        // التقارير
        public static readonly ApplicationPermission ViewReports = new("عرض التقارير", "reports.view", ReportsGroup, "شاشة «التقارير»: عرض الإحصائيات والتقارير وتصديرها");

        // القوائم المرجعية
        public static readonly ApplicationPermission ManageLookups = new("إدارة القوائم", "lookups.manage", LookupsGroup, "شاشات «الكليات، الأقسام، المباني، المستويات الدراسية، الفصول الدراسية، بنود التعهّد»: إضافة وتعديل وحذف");

        // إعدادات النظام
        public static readonly ApplicationPermission AdSetup = new("أدوات الأكتف دايركتوري", "system.adSetup", SystemGroup, "فحص جاهزية الاتصال بالأكتف دايركتوري وإنشاء مستخدم تجريبي حقيقي فيه - أداة تشخيص حسّاسة، لمدير النظام فقط");

        public static readonly ReadOnlyCollection<ApplicationPermission> All = new(new List<ApplicationPermission>
        {
            ViewUsers, ManageUsers, AddUserFromLdap,
            ViewRoles, ManageRoles, AssignRoles,
            ViewAuditLogs, ViewErrorLogs, ViewSignInLog,
            ViewStudents, CreateStudents, BulkImportStudents, EditStudents, DeleteStudents, ChangeStudentStatus, OverrideStudentStatus,
            ViewRequests, CreateRequests, RequestAttachments, ReviewHousing, ReviewCyber, CompleteRequests,
            ViewHousing, TransferHousing, ManageHousingAccounts, SyncAd,
            ViewReports,
            ManageLookups,
            AdSetup
        });
    }
}
