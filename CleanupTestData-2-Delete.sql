/* ============================================================================
   الخطوة ٢ من ٢ — الحذف الفعلي
   ----------------------------------------------------------------------------
   ⛔ ماتشغّلش الملف ده غير بعد:
        1) أخذ نسخة احتياطية كاملة من قاعدة البيانات
        2) تشغيل CleanupTestData-1-Report.sql ومراجعة الأرقام

   الحذف كله جوّه معاملة واحدة: لو أي خطوة فشلت، كل حاجة بترجع زي ما كانت
   ومايتمسحش ولا صف.

   الاستخدام: SSMS ← اتأكد إن قاعدة البيانات المختارة NUH_DB ← اضغط F5
   ============================================================================ */

USE NUH_DB;
GO

/* الترتيب من الأبناء للآباء عشان مفاتيح العلاقات ما تمنعش الحذف. */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY

    PRINT '===== بدء الحذف =====';

    /* --- مرفقات وإجراءات حالة الطالب ---
       ⚠️ من غير JOIN عن قصد: إحنا بنمسح كل الصفوف أصلاً، والـ JOIN كان هيحتاج
          أسماء الأعمدة الحقيقية (student_status_action_id مش StudentStatusActionId). */
    DELETE FROM dbo.StudentStatusAttachments;
    PRINT 'StudentStatusAttachments: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.StudentStatusActions;
    PRINT 'StudentStatusActions: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- تنقلات السكن --- */
    DELETE FROM dbo.HousingTransfers;
    PRINT 'HousingTransfers: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- كل ما يتعلق بالطلبات --- */
    DELETE FROM dbo.RequestAttachments;
    PRINT 'RequestAttachments: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.StudentDeclarations;
    PRINT 'StudentDeclarations: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.WorkflowHistory;
    PRINT 'WorkflowHistory: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.Notifications;
    PRINT 'Notifications: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.Requests;
    PRINT 'Requests: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- الرفع الجماعي --- */
    DELETE FROM dbo.BulkRequestStudents;
    PRINT 'BulkRequestStudents: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.BulkRequests;
    PRINT 'BulkRequests: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- سجل دورة حياة حسابات الشبكة --- */
    DELETE FROM dbo.AccountLifecycleLogs;
    PRINT 'AccountLifecycleLogs: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- رموز التحقق ورسائل SMS التجريبية --- */
    DELETE FROM dbo.OTPVerifications;
    PRINT 'OTPVerifications: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.SMSLogs;
    PRINT 'SMSLogs: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- سجل التغييرات المرتبط بالطلاب (تتبّع الأعمدة) --- */
    DELETE FROM dbo.AuditChangeLogs;
    PRINT 'AuditChangeLogs: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- سجل الإجراءات الخاص بالطلاب والطلبات فقط ---
       ⚠️ بنسيب سجلات تسجيل الدخول وإدارة المستخدمين والأدوار: دي سجل أمني
          للنظام نفسه مش بيانات تجربة، ومسحها بيضيّع أثر مين عمل إيه. */
    DELETE FROM dbo.AuditLogs
    WHERE target_table IN ('Students', 'Requests', 'HousingTransfers', 'StudentStatusActions', 'BulkRequests');
    PRINT 'AuditLogs (طلاب/طلبات): ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- الطلاب (آخر حاجة) --- */
    DELETE FROM dbo.Students;
    PRINT 'Students: ' + CAST(@@ROWCOUNT AS varchar(10));

    /* --- إعادة ترقيم الجداول من 1 عشان البيانات الجديدة تبدأ نظيفة --- */
    DBCC CHECKIDENT ('dbo.Students', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Requests', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.RequestAttachments', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.StudentStatusActions', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.StudentStatusAttachments', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.HousingTransfers', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.BulkRequests', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.BulkRequestStudents', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.WorkflowHistory', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Notifications', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.AccountLifecycleLogs', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.StudentDeclarations', RESEED, 0) WITH NO_INFOMSGS;

    COMMIT TRANSACTION;
    PRINT '===== تم التنظيف بنجاح =====';

END TRY
BEGIN CATCH

    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT '===== فشل التنظيف — تم التراجع عن كل شيء ولم تُحذف أي بيانات =====';
    PRINT 'رقم الخطأ: '  + CAST(ERROR_NUMBER() AS varchar(20));
    PRINT 'السطر: '      + CAST(ERROR_LINE()   AS varchar(20));
    PRINT 'الرسالة: '    + ERROR_MESSAGE();
    THROW;

END CATCH
GO

/* ---- تحقق بعد التنظيف ---- */
SELECT 'Students' AS [الجدول], COUNT(*) AS [المتبقي] FROM dbo.Students
UNION ALL SELECT 'Requests',             COUNT(*) FROM dbo.Requests
UNION ALL SELECT 'HousingTransfers',     COUNT(*) FROM dbo.HousingTransfers
UNION ALL SELECT 'StudentStatusActions', COUNT(*) FROM dbo.StudentStatusActions
UNION ALL SELECT 'BulkRequests',         COUNT(*) FROM dbo.BulkRequests;

-- المفروض دول ما اتغيّروش
SELECT 'Users' AS [يجب أن يبقى كما هو], COUNT(*) AS [العدد] FROM dbo.Users
UNION ALL SELECT 'Roles',           COUNT(*) FROM dbo.AspNetRoles
UNION ALL SELECT 'RoleClaims',      COUNT(*) FROM dbo.AspNetRoleClaims
UNION ALL SELECT 'Colleges',        COUNT(*) FROM dbo.Colleges
UNION ALL SELECT 'Departments',     COUNT(*) FROM dbo.Departments
UNION ALL SELECT 'Buildings',       COUNT(*) FROM dbo.Buildings
UNION ALL SELECT 'ADConfiguration', COUNT(*) FROM dbo.ADConfiguration;
GO
