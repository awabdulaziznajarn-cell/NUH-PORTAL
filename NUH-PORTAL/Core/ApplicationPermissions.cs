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
        public const string FacultyHousingGroup = "سكن أعضاء هيئة التدريس";
        public const string LookupsGroup = "القوائم المرجعية";
        public const string ReportsGroup = "التقارير";
        public const string SystemGroup = "إعدادات النظام";

        // المستخدمون
        public static readonly ApplicationPermission ViewUsers = new("عرض المستخدمين", "users.view", UsersGroup, "شاشة «المستخدمون»: عرض موظفي النظام وبياناتهم وأدوارهم (قراءة فقط)");
        public static readonly ApplicationPermission ManageUsers = new("إدارة المستخدمين", "users.manage", UsersGroup, "إضافة موظف، تعديل بياناته، تفعيل أو تعطيل حسابه، وضبط كلمة مروره");
        public static readonly ApplicationPermission AddUserFromLdap = new("إضافة من الدليل", "users.addFromLdap", UsersGroup, "زر «إضافة من الدليل»: سحب موظف موجود في Active Directory بدل كتابة بياناته يدويًا");
        public static readonly ApplicationPermission DeleteUsers = new("حذف واستعادة مستخدم", "users.delete", UsersGroup, "حذف حساب موظف: يختفي من الشاشة ويُمنع من الدخول، ويبقى اسمه منسوبًا لإجراءاته في سجل الإجراءات - ويمكن استعادته. أخطر من التعطيل، يُفضّل قصره على مدير النظام");

        // الأدوار والصلاحيات
        public static readonly ApplicationPermission ViewRoles = new("عرض الأدوار", "roles.view", RolesGroup, "شاشة «الأدوار والصلاحيات»: عرض الأدوار وصلاحيات كل دور (قراءة فقط)");
        public static readonly ApplicationPermission ManageRoles = new("إدارة الأدوار", "roles.manage", RolesGroup, "إنشاء دور، تعديل صلاحياته، حذفه - أخطر صلاحية في النظام، إذ تتيح لصاحبها توسيع صلاحيات أي مستخدم");
        public static readonly ApplicationPermission AssignRoles = new("إسناد الأدوار", "roles.assign", RolesGroup, "تغيير دور الموظف من شاشة المستخدمين (مشرف / أمن سيبراني / مدير النظام)");

        // السجلّات
        // ⚠️ الاسم هنا لازم يطابق اسم البند في القائمة الجانبية بالحرف. كان
        //    «سجل الإجراءات» والقائمة بتقول «سجل العمليات»، فالمسؤول يدوّر في
        //    شاشة الأدوار على اسم القائمة ومايلاقيهوش، ويفتكر إن الصفحة دي
        //    مالهاش صلاحية أصلًا. القيمة (auditLogs.view) ما اتغيّرتش، فمفيش
        //    دور هيفقد صلاحيته.
        public static readonly ApplicationPermission ViewAuditLogs = new("سجل العمليات", "auditLogs.view", LogsGroup, "شاشة «سجل العمليات» في القائمة الجانبية: من نفّذ الإجراء ومتى - تسجيل، نقل، اعتماد، رفض، تغيير حالة. بدونها تختفي الصفحة من القائمة ويُرفض فتحها بالرابط");
        public static readonly ApplicationPermission ViewAllAuditLogs = new("سجل العمليات - كل الإدارات", "auditLogs.viewAll", LogsGroup, "بلا هذه الصلاحية يرى الموظف إجراءات إدارته وإجراءات الطلاب فقط، ولا يرى إجراءات موظفي الإدارات الأخرى. امنحها لمن يحتاج صورة النظام كاملة");
        public static readonly ApplicationPermission ViewErrorLogs = new("سجل الأخطاء", "errorLogs.view", LogsGroup, "شاشة «سجل الأخطاء»: أخطاء النظام التقنية - للدعم الفني");
        public static readonly ApplicationPermission ViewSignInLog = new("سجل الدخول والخروج", "signInLog.view", LogsGroup, "شاشة «سجل الدخول»: محاولات الدخول الناجحة والفاشلة ووقتها وجهازها");

        // الطلاب
        public static readonly ApplicationPermission ViewStudents = new("عرض الطلاب", "students.view", StudentsGroup, "شاشة «الطلاب»: البحث والاطلاع على بيانات الطالب وسكنه وحالته (قراءة فقط)");
        public static readonly ApplicationPermission CreateStudents = new("تسجيل طالب", "students.create", StudentsGroup, "شاشة «تسجيل طالب»: إدخال طالب واحد يدويًا بكامل بياناته وسكنه");
        public static readonly ApplicationPermission BulkImportStudents = new("الرفع الجماعي (إكسل)", "students.bulkImport", StudentsGroup, "شاشة «تسجيل جماعي (Excel)» في القائمة الجانبية: تنزيل القالب ورفع ملف إكسل بعشرات الطلاب مرة واحدة");
        public static readonly ApplicationPermission EditStudents = new("تعديل بيانات طالب", "students.edit", StudentsGroup, "تعديل بيانات طالب مسجّل (الاسم، الجوال، الكلية، السكن) من شاشة الطلاب");
        public static readonly ApplicationPermission DeleteStudents = new("حذف واستعادة طالب", "students.delete", StudentsGroup, "حذف سجل طالب من النظام أو استعادته بعد الحذف - إجراء حسّاس، يُفضّل قصره على مدير النظام");
        public static readonly ApplicationPermission ChangeStudentStatus = new("تحديث حالة الطالب", "students.changeStatus", StudentsGroup, "شاشة «تحديث حالة الطالب»: تسجيل تخرّج / فصل / تحويل / ترك السكن. تنبيه: التخرّج والفصل والتحويل تُعطِّل حساب الشبكة تلقائيًا");
        public static readonly ApplicationPermission OverrideStudentStatus = new("تصحيح حالة نهائية", "students.overrideStatus", StudentsGroup, "تسجيل حالة نهائية جديدة لطالب حالته النهائية مسجّلة بالفعل - لتصحيح إدخال خاطئ فقط، يُفضّل قصره على مدير النظام");
        // ⚠️ صلاحية مستقلة عن students.view عن قصد: شاشة «ملف الطالب» بتجمع في
        //    صفحة واحدة بياناته الشخصية والأكاديمية وسكنه وحساب شبكته وتعهّده
        //    الموقّع - وده أوسع من أي شاشة تانية في النظام. لو اتربطت بصلاحية
        //    قايمة، كل من عنده الصلاحية دي كان هياخد الملف المجمّع معاها من غير
        //    ما حد يقصد. وكل فتح للملف بيتسجّل في سجل العمليات.
        public static readonly ApplicationPermission InvestigateStudents = new("ملف الطالب (تحقيقي)", "students.investigate", StudentsGroup, "شاشة «ملف الطالب»: البحث برقم جامعي أو رقم هوية وعرض بيانات الطالب وطلبه وتعهّده الموقّع في صفحة واحدة قابلة للطباعة. كل عملية فتح تُسجَّل في سجل العمليات باسم المستخدم");

        public static readonly ApplicationPermission AllGenders = new("الطلاب والطالبات معًا", "students.allGenders", StudentsGroup, "رؤية الطلاب والطالبات وطلباتهم معًا. بدونها يرى الموظف القسم المحدَّد في حسابه فقط (طلاب أو طالبات)، وطلب الطالبة يظهر لمشرفة قسم الطالبات وحدها. امنحها لمن يحتاج القسمين - مدير النظام والأمن السيبراني");

        // الطلبات
        public static readonly ApplicationPermission ViewRequests = new("عرض الطلبات", "requests.view", RequestsGroup, "شاشة «كل الطلبات» في القائمة الجانبية: عرض الطلبات وتفاصيلها ومسارها - يرى كل مستخدم الطلبات الواقعة في مرحلته وفق صلاحيات المراجعة");
        public static readonly ApplicationPermission CreateRequests = new("تقديم طلب نيابة عن طالب", "requests.create", RequestsGroup, "إنشاء طلب سكن لطالب من داخل النظام بدلًا من أن يقدّمه الطالب بنفسه");
        public static readonly ApplicationPermission ReviewHousing = new("مراجعة إدارة الإسكان", "requests.reviewHousing", RequestsGroup, "المرحلة الأولى: اعتماد أو رفض أو طلب معلومات إضافية على الطلب وهو عند إدارة الإسكان");
        public static readonly ApplicationPermission ReviewCyber = new("مراجعة الأمن السيبراني", "requests.reviewCyber", RequestsGroup, "المرحلة الثانية: اعتماد أو رفض الطلب بعد موافقة إدارة الإسكان عليه وإحالته إلى الأمن السيبراني");
        public static readonly ApplicationPermission CompleteRequests = new("إكمال الطلب وإنشاء الحساب", "requests.complete", RequestsGroup, "المرحلة الأخيرة: إغلاق الطلب وإنشاء حساب الشبكة للطالب فعليًا في Active Directory");

        // السكن وحسابات الشبكة
        public static readonly ApplicationPermission ViewHousing = new("عرض السكن والحسابات", "housing.view", HousingGroup, "شاشة «إدارة حسابات السكن» في القائمة الجانبية: عرض المباني والوحدات وحسابات الشبكة وحالتها (قراءة فقط)");
        public static readonly ApplicationPermission TransferHousing = new("نقل سكن طالب", "housing.transfer", HousingGroup, "نقل طالب من مبنى أو دور أو شقة أو غرفة إلى أخرى مع إرفاق مستند النقل");
        public static readonly ApplicationPermission ManageHousingAccounts = new("إدارة حسابات الشبكة", "housing.manageAccounts", HousingGroup, "تفعيل أو تعطيل حساب الطالب في الشبكة، تصفير كلمة مروره، وإعادة إنشاء الحساب");
        public static readonly ApplicationPermission SyncAd = new("مزامنة وإعدادات الأكتف دايركتوري", "housing.syncAd", HousingGroup, "زر «مزامنة حسابات الشبكة» وتعديل إعدادات الاتصال بالأكتف دايركتوري (مسارات الـ OU والمجموعات)");

        // سكن أعضاء هيئة التدريس
        // ⚠️ منفصلة تمامًا عن صلاحيات الطلاب: سكن أعضاء هيئة التدريس أبراج وفلل
        //    ما لها طلبات ولا تعهّدات ولا حساب طالب. من يدير سكن الطلاب لا يلزم
        //    أن يرى بيانات أعضاء هيئة التدريس، والعكس صحيح.
        public static readonly ApplicationPermission ViewFacultyHousing = new("عرض وحدات أعضاء هيئة التدريس", "facultyHousing.view", FacultyHousingGroup, "شاشة «الوحدات»: الأبراج والفلل وحالتها والساكن الحالي وسجل كل وحدة (قراءة فقط)");
        public static readonly ApplicationPermission ManageFacultyHousing = new("تسليم وحدة وتعديل بياناتها", "facultyHousing.manage", FacultyHousingGroup, "تسليم وحدة لساكن جديد، وإغلاق إشغال ساكن غادر، وتعديل حالة الوحدة (خارج الخدمة / غير موجودة). يسجّل في النظام ولا يكتب في الدومين وحده");
        public static readonly ApplicationPermission SyncFacultyHousing = new("الكتابة في الدومين", "facultyHousing.syncAd", FacultyHousingGroup, "زر «تطبيق على الدومين»: كتابة اسم الساكن ورقم هويته وجواله في حساب الوحدة، وتعطيل الحساب أو تفعيله. أخطر صلاحية في هذا القسم - أثرها خارج النظام");
        public static readonly ApplicationPermission ImportFacultyHousing = new("الاستيراد من الدومين", "facultyHousing.import", FacultyHousingGroup, "قراءة وحدات الأبراج والفلل من الـ OU الخاصة بها في الدومين وتسجيلها في النظام. تُستخدم مرة عند التأسيس وعند إضافة وحدات جديدة");
        public static readonly ApplicationPermission ConfirmFacultyOccupancy = new("تأكيد الإشغال", "facultyHousing.confirm", FacultyHousingGroup, "شاشة «تأكيد الإشغال»: التعليم بأن الساكن ما زال موجودًا أو أنه غادر. صلاحية محدودة تُمنح لإدارة الإسكان - لا تتيح تعديل بيانات ولا كتابة في الدومين");

        // التقارير
        public static readonly ApplicationPermission ViewReports = new("عرض التقارير", "reports.view", ReportsGroup, "شاشة «التقارير»: عرض الإحصائيات والتقارير وتصديرها");

        // القوائم المرجعية
        public static readonly ApplicationPermission ManageLookups = new("إدارة القوائم", "lookups.manage", LookupsGroup, "شاشات «الكليات، الأقسام، المباني، المستويات الدراسية، الفصول الدراسية، بنود التعهّد»: إضافة وتعديل وحذف");

        // إعدادات النظام
        public static readonly ApplicationPermission AdSetup = new("أدوات الأكتف دايركتوري", "system.adSetup", SystemGroup, "فحص جاهزية الاتصال بالأكتف دايركتوري وإنشاء مستخدم تجريبي حقيقي فيه - أداة تشخيص حسّاسة، لمدير النظام فقط");

        // ⚠️ أي صلاحية مش مذكورة في القائمة دي مالهاش policy في Program.cs،
        //    ومحدش يقدر ياخدها — لا الأدمن من DbSeeder ولا من شاشة الأدوار
        //    (RoleAdminService بيفلتر بالقائمة دي). كانت ViewAllAuditLogs
        //    ناقصة، فخيار «كل الإدارات» في سجل الإجراءات كان معطّلًا نهائيًا
        //    وغير قابل للتفعيل من أي مكان.
        public static readonly ReadOnlyCollection<ApplicationPermission> All = new(new List<ApplicationPermission>
        {
            ViewUsers, ManageUsers, AddUserFromLdap, DeleteUsers,
            ViewRoles, ManageRoles, AssignRoles,
            ViewAuditLogs, ViewAllAuditLogs, ViewErrorLogs, ViewSignInLog,
            ViewStudents, CreateStudents, BulkImportStudents, EditStudents, DeleteStudents, ChangeStudentStatus, OverrideStudentStatus, AllGenders, InvestigateStudents,
            ViewRequests, CreateRequests, ReviewHousing, ReviewCyber, CompleteRequests,
            ViewHousing, TransferHousing, ManageHousingAccounts, SyncAd,
            ViewFacultyHousing, ManageFacultyHousing, SyncFacultyHousing, ImportFacultyHousing, ConfirmFacultyOccupancy,
            ViewReports,
            ManageLookups,
            AdSetup
        });
    }
}
