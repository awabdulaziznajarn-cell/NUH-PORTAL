/* ============================================================================
   تنظيف بيانات التجربة — NUH-PORTAL
   ----------------------------------------------------------------------------
   الغرض: مسح الطلاب التجريبيين وكل ما يتعلق بهم (طلبات، مرفقات، تنقلات،
          إجراءات حالة، إشعارات، سجلات دورة حياة الحساب) استعدادًا لرفع
          بيانات إدارة الإسكان الحقيقية.

   ⚠️ قبل التشغيل:
      1) خُذ نسخة احتياطية كاملة من قاعدة البيانات. السكربت لا رجعة فيه.
         BACKUP DATABASE NUH_DB TO DISK = 'D:\NUH-DATA\Backup\NUH_DB_before_cleanup.bak' WITH INIT;
      2) شغّل «المرحلة ١» لوحدها أولاً واقرأ الأرقام. لو رقم مش متوقّع، توقّف.
      3) السكربت لا يمسّ: المستخدمين، الأدوار، الصلاحيات، القوائم المرجعية
         (الكليات/الأقسام/المباني/المستويات/الفصول)، إعدادات الأكتف دايركتوري.

   طريقة التشغيل: SSMS ← قاعدة البيانات NUH_DB ← نفّذ المرحلة ١، راجع، ثم المرحلة ٢.
   ============================================================================ */

USE NUH_DB;
GO

/* ============================================================================
   المرحلة ١ — تقرير: إيه اللي هيتمسح؟ (قراءة فقط، مفيش أي تعديل)
   ============================================================================ */

PRINT '===== المرحلة ١: تقرير ما سيُحذف =====';

SELECT 'Students'                AS [الجدول], COUNT(*) AS [عدد الصفوف] FROM dbo.Students
UNION ALL SELECT 'Requests',                 COUNT(*) FROM dbo.Requests
UNION ALL SELECT 'RequestAttachments',       COUNT(*) FROM dbo.RequestAttachments
UNION ALL SELECT 'StudentDeclarations',      COUNT(*) FROM dbo.StudentDeclarations
UNION ALL SELECT 'WorkflowHistory',          COUNT(*) FROM dbo.WorkflowHistory
UNION ALL SELECT 'Notifications',            COUNT(*) FROM dbo.Notifications
UNION ALL SELECT 'StudentStatusActions',     COUNT(*) FROM dbo.StudentStatusActions
UNION ALL SELECT 'StudentStatusAttachments', COUNT(*) FROM dbo.StudentStatusAttachments
UNION ALL SELECT 'HousingTransfers',         COUNT(*) FROM dbo.HousingTransfers
UNION ALL SELECT 'BulkRequests',             COUNT(*) FROM dbo.BulkRequests
UNION ALL SELECT 'BulkRequestStudents',      COUNT(*) FROM dbo.BulkRequestStudents
UNION ALL SELECT 'AccountLifecycleLogs',     COUNT(*) FROM dbo.AccountLifecycleLogs
UNION ALL SELECT 'OTPVerifications',         COUNT(*) FROM dbo.OTPVerifications
UNION ALL SELECT 'SMSLogs',                  COUNT(*) FROM dbo.SMSLogs;

-- الطلاب اللي هيتمسحوا، وحسابات الشبكة المرتبطة بيهم
PRINT '--- الطلاب ---';
SELECT Id, student_id, full_name, full_name_english, ad_username, ad_status, housing_building, floor_number, apartment_number, room_number
FROM dbo.Students
ORDER BY student_id;

-- ⚠️ اقرا العمود ده: دي حسابات موجودة في الأكتف دايركتوري ومربوطة بالنظام.
--    مسحها من قاعدة البيانات *مابيمسحهاش* من الدومين — لازم تشيلها بنفسك
--    (سكربت PowerShell المرفق) وإلا هتفضل موجودة هناك.
PRINT '--- حسابات الشبكة المربوطة (شيلها من الأكتف دايركتوري يدويًا) ---';
SELECT student_id, ad_username, ad_status
FROM dbo.Students
WHERE ad_username IS NOT NULL AND ad_username <> ''
ORDER BY student_id;

-- مسارات المرفقات — احتاجها عشان تمسح الملفات من D:\NUH-DATA بعد كده
PRINT '--- مرفقات الطلبات ---';
SELECT Id, RequestId, FileName, OriginalFileName, UploadedAt FROM dbo.RequestAttachments ORDER BY Id;

PRINT '--- مرفقات إجراءات الحالة ---';
SELECT Id, StudentStatusActionId, FileName, OriginalFileName, UploadedAt FROM dbo.StudentStatusAttachments ORDER BY Id;

PRINT '--- مرفقات النقل ---';
SELECT Id, StudentId, StudentNumber, AttachmentPath FROM dbo.HousingTransfers WHERE AttachmentPath IS NOT NULL ORDER BY Id;

GO

/* ============================================================================
   المرحلة ٢ — الحذف الفعلي
   ----------------------------------------------------------------------------
   ⚠️ ماتشغّلهاش غير بعد ما تراجع أرقام المرحلة ١ وتاخد باك أب.
      كل الحذف جوّه معاملة واحدة: لو أي خطوة فشلت، كل حاجة بترجع زي ما كانت.

   الترتيب من الأبناء للآباء عشان مفاتيح العلاقات ما تمنعش الحذف.
   ============================================================================ */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY

    PRINT '===== المرحلة ٢: بدء الحذف =====';

    /* --- مرفقات وإجراءات حالة الطالب --- */
    DELETE a
    FROM dbo.StudentStatusAttachments a
    INNER JOIN dbo.StudentStatusActions s ON s.Id = a.StudentStatusActionId;
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

/* ============================================================================
   المرحلة ٣ — تحقق بعد التنظيف
   ============================================================================ */
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
