/* ==================================================================
   NUH-PORTAL - تفريغ بيانات الطلاب والطالبات (بيانات التجربة)
   ------------------------------------------------------------------
   ⚠️⚠️ السكربت ده **بيمسح بيانات**. اقراه كله قبل ما تشغّله.

   بيمسح: الطلاب وكل اللي متعلّق بيهم - الطلبات والتعهّدات ومسار الطلب
          والإشعارات وسجل السكن وسجل دورة حياة الحساب وتغييرات الحالة
          ومرفقاتها، ورفعات الإكسل، ورموز التحقّق ورسائل الجوال.

   ما بيمسحش: المستخدمين والأدوار والصلاحيات، المباني والكليات والأقسام
          والمستويات والفصول، بنود التعهّد، إعدادات الدومين، سجل الدخول،
          سجل الأخطاء، سكن أعضاء هيئة التدريس - كلها بتفضل زي ما هي.

   ⚠️ حاجتان **مش** بيوصّلهم السكربت ده، ولازم تتعملوا بإيدك:
      ١) حسابات الشبكة في الدومين (h + الرقم الجامعي). مسح الصفّ من
         قاعدة البيانات مابيلمسش الأكتف دايركتوري - الحساب هيفضل موجود
         هناك، ولو طالب حقيقي سجّل بنفس الرقم بعدين، النظام هيلاقي حسابًا
         قائمًا. امسحهم أو عطّلهم من الـ OU بتاعة السكن الأول.
      ٢) مرفقات الطلاب على الديسك (مجلد Students تحت مسار المرفقات في
         appsettings). امسح المجلد بإيدك لو عايز البداية نضيفة.

   ⚠️ الترتيب مش اعتباطي: الجداول الأبناء الأول، وإلا المفاتيح الأجنبية
      بترفض المسح. والكل جوّه معاملة واحدة - يا كله يا ولا حاجة.

   ⚠️ الـ COMMIT **متعلّق عليه** في الآخر. شغّل، بصّ على الأعداد اللي
      هتطلع، وبعدين شيل التعليق عنه وشغّله لوحده.
   ================================================================== */
USE NUH_DB;
GO

/* ---------- (أ) شوف الأول: إيه اللي هيتمسح ---------- */
SELECT 'Students'                 AS TableName, COUNT(*) AS Rows FROM dbo.Students
UNION ALL SELECT 'Requests',                COUNT(*) FROM dbo.Requests
UNION ALL SELECT 'StudentDeclarations',     COUNT(*) FROM dbo.StudentDeclarations
UNION ALL SELECT 'WorkflowHistory',         COUNT(*) FROM dbo.WorkflowHistory
UNION ALL SELECT 'Notifications',           COUNT(*) FROM dbo.Notifications
UNION ALL SELECT 'HousingTransfers',        COUNT(*) FROM dbo.HousingTransfers
UNION ALL SELECT 'AccountLifecycleLogs',    COUNT(*) FROM dbo.AccountLifecycleLogs
UNION ALL SELECT 'StudentStatusActions',    COUNT(*) FROM dbo.StudentStatusActions
UNION ALL SELECT 'StudentStatusAttachments',COUNT(*) FROM dbo.StudentStatusAttachments
UNION ALL SELECT 'BulkRequests',            COUNT(*) FROM dbo.BulkRequests
UNION ALL SELECT 'BulkRequestStudents',     COUNT(*) FROM dbo.BulkRequestStudents
UNION ALL SELECT 'OTPVerifications',        COUNT(*) FROM dbo.OTPVerifications
UNION ALL SELECT 'SMSLogs',                 COUNT(*) FROM dbo.SMSLogs;
GO

/* ---------- (ب) قائمة حسابات الشبكة اللي هتفضل في الدومين ----------
   احتفظ بالناتج ده قبل المسح: بعد ما الصفوف تروح مافيش حاجة تفكّرك
   بالحسابات اللي اتعملت فعلًا في الأكتف دايركتوري.                    */
SELECT student_id, full_name, ad_username, ad_status
FROM dbo.Students
WHERE ad_username IS NOT NULL AND LTRIM(RTRIM(ad_username)) <> ''
ORDER BY student_id;
GO

/* ---------- (ج) المسح ---------- */
BEGIN TRANSACTION;

/* أبناء StudentStatusActions */
DELETE FROM dbo.StudentStatusAttachments;
PRINT CONCAT('StudentStatusAttachments -> ', @@ROWCOUNT);

DELETE FROM dbo.StudentStatusActions;
PRINT CONCAT('StudentStatusActions     -> ', @@ROWCOUNT);

/* أبناء Requests */
DELETE FROM dbo.StudentDeclarations;
PRINT CONCAT('StudentDeclarations      -> ', @@ROWCOUNT);

DELETE FROM dbo.WorkflowHistory;
PRINT CONCAT('WorkflowHistory          -> ', @@ROWCOUNT);

DELETE FROM dbo.Notifications;
PRINT CONCAT('Notifications            -> ', @@ROWCOUNT);

/* أبناء Students */
DELETE FROM dbo.HousingTransfers;
PRINT CONCAT('HousingTransfers         -> ', @@ROWCOUNT);

DELETE FROM dbo.AccountLifecycleLogs;
PRINT CONCAT('AccountLifecycleLogs     -> ', @@ROWCOUNT);

DELETE FROM dbo.Requests;
PRINT CONCAT('Requests                 -> ', @@ROWCOUNT);

/* الطلاب نفسهم */
DELETE FROM dbo.Students;
PRINT CONCAT('Students                 -> ', @@ROWCOUNT);

/* رفعات الإكسل - صفوفها بيانات طلاب برضه */
DELETE FROM dbo.BulkRequestStudents;
PRINT CONCAT('BulkRequestStudents      -> ', @@ROWCOUNT);

DELETE FROM dbo.BulkRequests;
PRINT CONCAT('BulkRequests             -> ', @@ROWCOUNT);

/* رموز التحقّق ورسائل الجوال - كلها بتخصّ الطلاب (الموظف بيدخل باسم مستخدم) */
DELETE FROM dbo.OTPVerifications;
PRINT CONCAT('OTPVerifications         -> ', @@ROWCOUNT);

DELETE FROM dbo.SMSLogs;
PRINT CONCAT('SMSLogs                  -> ', @@ROWCOUNT);

/* ---------- ترقيم جديد يبدأ من ١ ----------
   ⚠️ مش تجميل: أرقام الطلبات والوثائق بتتبني على الـ Id، فبداية نضيفة
      معناها إن أول طلب حقيقي رقمه ١ لا ٤٧٣ - وده اللي بيتطبع على
      الوثيقة وبيتقال للطالب على التليفون.
   ⚠️ و RESEED بـ 0 مع DBCC مابيكسرش حاجة هنا: الجداول فاضية دلوقتي. */
DBCC CHECKIDENT ('dbo.Students',                 RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Requests',                 RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.StudentDeclarations',      RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.WorkflowHistory',          RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Notifications',            RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.HousingTransfers',         RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.AccountLifecycleLogs',     RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.StudentStatusActions',     RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.StudentStatusAttachments', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.BulkRequests',             RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.BulkRequestStudents',      RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.OTPVerifications',         RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.SMSLogs',                  RESEED, 0) WITH NO_INFOMSGS;
PRINT 'Identity counters reseeded to 0';

-- راجع الأعداد فوق، وبعدين شيل التعليق عن السطر اللي تحت وشغّله:
-- COMMIT TRANSACTION;
-- ولو مش عاجبك:
-- ROLLBACK TRANSACTION;
GO

/* ==================================================================
   (د) اختياري - سجل العمليات
   ------------------------------------------------------------------
   ⚠️ سايبه من غير مسح **عن قصد**. سجل العمليات هو اللي بيقول مين عمل
      إيه، والصفوف اللي فيه بتخصّ موظفين حقيقيين بيجرّبوا النظام - مش
      بيانات طلاب. مسحه بيمسح دليل التجربة نفسها.

      ولو قرّرت تبدأ بسجل نضيف تمامًا، شيل التعليق عن التلات سطور دي:  */
-- BEGIN TRANSACTION;
-- DELETE FROM dbo.AuditChangeLogs;
-- DELETE FROM dbo.AuditLogs;
-- COMMIT TRANSACTION;

/* ==================================================================
   (هـ) بعد المسح - اتأكد
   ================================================================== */
SELECT COUNT(*) AS StudentsLeft FROM dbo.Students;
SELECT COUNT(*) AS RequestsLeft FROM dbo.Requests;
GO
