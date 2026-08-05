/* ============================================================================
   الخطوة ١ من ٢ — تقرير: إيه اللي هيتمسح؟
   ----------------------------------------------------------------------------
   ⚠️ الملف ده *قراءة فقط*. مفيش فيه ولا أمر حذف واحد. شغّله كله بأمان.
      الحذف في الملف التاني: CleanupTestData-2-Delete.sql

   الاستخدام: SSMS ← اتأكد إن قاعدة البيانات المختارة NUH_DB ← اضغط F5
   ============================================================================ */

USE NUH_DB;
GO

PRINT '===== تقرير ما سيُحذف =====';

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

-- مسارات المرفقات — احتاجها عشان تمسح الملفات من D:\NUH-DATA بعد كده.
-- ⚠️ SELECT * عن قصد: أسماء الأعمدة في الجداول دي snake_case (request_id،
--    file_name، uploaded_at ...) مش زي أسماء الخصائص في الكود، فتسمية الأعمدة
--    يدويًا كانت بترمي "Invalid column name".
PRINT '--- مرفقات الطلبات ---';
SELECT * FROM dbo.RequestAttachments;

PRINT '--- مرفقات إجراءات الحالة ---';
SELECT * FROM dbo.StudentStatusAttachments;

PRINT '--- تنقلات السكن ومرفقاتها ---';
SELECT * FROM dbo.HousingTransfers;

PRINT '--- إجراءات حالة الطالب ---';
SELECT * FROM dbo.StudentStatusActions;

GO
