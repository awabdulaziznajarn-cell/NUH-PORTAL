using System.Collections.ObjectModel;

namespace NUH_PORTAL.Core
{
    // كل صلاحيات النظام معرّفة في مكان واحد ومقسّمة لمجموعات (زي الـ permit).
    // كل صلاحية = policy بتتسجّل في Program.cs، والكنترولر بيستخدمها: [Authorize(Policy = "users.manage")].
    public static class ApplicationPermissions
    {
        public const string UsersGroup = "المستخدمون";
        public const string RolesGroup = "الأدوار والصلاحيات";
        public const string LogsGroup = "السجلّات";
        public const string StudentsGroup = "الطلاب";
        public const string RequestsGroup = "الطلبات";
        public const string HousingGroup = "السكن";
        public const string LookupsGroup = "القوائم المرجعية";

        // المستخدمون
        public static readonly ApplicationPermission ViewUsers = new("عرض المستخدمين", "users.view", UsersGroup, "الاطلاع على حسابات المستخدمين");
        public static readonly ApplicationPermission ManageUsers = new("إدارة المستخدمين", "users.manage", UsersGroup, "إنشاء وتعديل وتعطيل المستخدمين");
        public static readonly ApplicationPermission AddUserFromLdap = new("إضافة من الدليل", "users.addFromLdap", UsersGroup, "إضافة مستخدم من Active Directory");

        // الأدوار والصلاحيات
        public static readonly ApplicationPermission ViewRoles = new("عرض الأدوار", "roles.view", RolesGroup, "الاطلاع على الأدوار والصلاحيات");
        public static readonly ApplicationPermission ManageRoles = new("إدارة الأدوار", "roles.manage", RolesGroup, "إنشاء وتعديل وحذف الأدوار وصلاحياتها");
        public static readonly ApplicationPermission AssignRoles = new("إسناد الأدوار", "roles.assign", RolesGroup, "إسناد الأدوار للمستخدمين");

        // السجلّات
        public static readonly ApplicationPermission ViewAuditLogs = new("سجل الإجراءات", "auditLogs.view", LogsGroup, "الاطلاع على سجل إجراءات المستخدمين");
        public static readonly ApplicationPermission ViewErrorLogs = new("سجل الأخطاء", "errorLogs.view", LogsGroup, "الاطلاع على سجل الأخطاء المرصودة");
        public static readonly ApplicationPermission ViewSignInLog = new("سجل الدخول والخروج", "signInLog.view", LogsGroup, "الاطلاع على سجل دخول وخروج المستخدمين");

        // الطلاب
        public static readonly ApplicationPermission ViewStudents = new("عرض الطلاب", "students.view", StudentsGroup, "الاطلاع على بيانات الطلاب");
        public static readonly ApplicationPermission ManageStudents = new("إدارة الطلاب", "students.manage", StudentsGroup, "إضافة وتعديل وحذف الطلاب");

        // الطلبات
        public static readonly ApplicationPermission ViewRequests = new("عرض الطلبات", "requests.view", RequestsGroup, "الاطلاع على الطلبات");
        public static readonly ApplicationPermission ProcessRequests = new("معالجة الطلبات", "requests.process", RequestsGroup, "اعتماد ورفض ومعالجة الطلبات");

        // السكن
        public static readonly ApplicationPermission ViewHousing = new("عرض السكن", "housing.view", HousingGroup, "الاطلاع على حسابات السكن");
        public static readonly ApplicationPermission ManageHousing = new("إدارة السكن", "housing.manage", HousingGroup, "إدارة حسابات السكن والنقل");

        // القوائم المرجعية
        public static readonly ApplicationPermission ManageLookups = new("إدارة القوائم", "lookups.manage", LookupsGroup, "إدارة القوائم المرجعية وبنود التعهّد");

        public static readonly ReadOnlyCollection<ApplicationPermission> All = new(new List<ApplicationPermission>
        {
            ViewUsers, ManageUsers, AddUserFromLdap,
            ViewRoles, ManageRoles, AssignRoles,
            ViewAuditLogs, ViewErrorLogs, ViewSignInLog,
            ViewStudents, ManageStudents,
            ViewRequests, ProcessRequests,
            ViewHousing, ManageHousing,
            ManageLookups
        });
    }
}
