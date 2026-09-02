/* ============================================================================
   تفريغ بيانات الطلاب والطالبات التجريبية - مع الإبقاء على سكن أعضاء هيئة
   التدريس كما هو.

   ⚠️ اقرأ قبل التشغيل:

   1) خُذ نسخة احتياطية كاملة من قاعدة البيانات أولًا. هذا السكربت لا يمكن
      التراجع عنه بعد COMMIT.

   2) هذا السكربت يمسّ **قاعدة البيانات وحدها**. لا يمسّ:
        • حسابات الطلاب في Active Directory - تبقى في الدليل بعد حذف صفوفها
          من النظام، فتصير حسابات يتيمة لا يعرفها أحد. احذفها من الدليل
          (أو من شاشة إدارة حسابات الإسكان قبل تشغيل السكربت).
        • ملفات المرفقات على القرص - المسار في appsettings.json تحت
          Storage:AttachmentsRoot، وافتراضيًّا D:\NUH-DATA. احذف ما تحته
          يدويًّا بعد التشغيل.

   3) ما **لا** يمسّه السكربت إطلاقًا: FacultyUnits و FacultyOccupancies
      (سكن أعضاء هيئة التدريس)، والمستخدمون والأدوار والصلاحيات، والقوائم
      المرجعية (الكليات، الأقسام، المباني، المستويات، بنود التعهّد)، وإعدادات
      الدليل النشط.

   4) الترتيب مقصود: الأبناء قبل الآباء. تغييره يوقع السكربت على قيود
      المفاتيح الأجنبية.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- أي خطأ يُرجع المعاملة كاملة لا نصفها

BEGIN TRANSACTION;

/* -------- قبل: عدّ ما سيُحذف، ليظهر في نتيجة التشغيل -------- */
SELECT 'قبل الحذف' AS المرحلة,
       (SELECT COUNT(*) FROM Students)              AS الطلاب,
       (SELECT COUNT(*) FROM Requests)              AS الطلبات,
       (SELECT COUNT(*) FROM StudentDeclarations)   AS التعهدات,
       (SELECT COUNT(*) FROM StudentStatusActions)  AS إجراءات_الحالة,
       (SELECT COUNT(*) FROM FacultyUnits)          AS وحدات_هيئة_التدريس,
       (SELECT COUNT(*) FROM FacultyOccupancies)    AS إشغالات_هيئة_التدريس;

/* ============================================================================
   ١ - أبناء إجراءات الحالة الأكاديمية
   ============================================================================ */
DELETE FROM StudentStatusAttachments;
DELETE FROM StudentStatusActions;

/* ============================================================================
   ٢ - نقل السكن وسجل دورة حياة الحساب
   ============================================================================ */
DELETE FROM HousingTransfers;
DELETE FROM AccountLifecycleLogs;

/* ============================================================================
   ٣ - كل ما يتعلّق بالطلب: التعهّد وسجل المراحل والإشعارات
   ⚠️ Notifications مربوطة بالطلب (request_id) لا بالطالب، فتُحذف قبل Requests.
   ============================================================================ */
DELETE FROM StudentDeclarations;
DELETE FROM WorkflowHistory;
DELETE FROM Notifications;

/* ============================================================================
   ٤ - الطلبات، ثم الرفع الجماعي الذي أنشأها
   ============================================================================ */
DELETE FROM Requests;
DELETE FROM BulkRequestStudents;
DELETE FROM BulkRequests;

/* ============================================================================
   ٥ - الطلاب والطالبات
   ============================================================================ */
DELETE FROM Students;

/* ============================================================================
   ٦ - مسارات التسجيل الذاتي: رموز التحقّق والرسائل
   ============================================================================ */
DELETE FROM OTPVerifications;
DELETE FROM SMSLogs;

/* ============================================================================
   ٧ - سجل العمليات: صفوف الطلاب والطلبات فقط
   ⚠️ مصفّى لا مفرَّغ: نفس الجدول يحمل عمليات سكن أعضاء هيئة التدريس
      (FacultyUnits / FacultyOccupancies) وعمليات المستخدمين، وهذه تبقى.
   ⚠️ والأبناء أولًا: AuditChangeLogs مربوطة بـ AuditLogs.
   ============================================================================ */
DECLARE @StudentTables TABLE (t NVARCHAR(64));
INSERT INTO @StudentTables (t) VALUES
  ('Students'), ('Requests'), ('StudentDeclarations'), ('StudentStatusActions'),
  ('StudentStatusAttachments'), ('HousingTransfers'), ('BulkRequests'),
  ('BulkRequestStudents'), ('AccountLifecycleLogs'), ('Notifications');

DELETE c
FROM AuditChangeLogs c
JOIN AuditLogs a ON a.Id = c.AuditLogId
WHERE a.target_table IN (SELECT t FROM @StudentTables);

DELETE FROM AuditLogs
WHERE target_table IN (SELECT t FROM @StudentTables);

/* ============================================================================
   ٨ - إعادة ترقيم المعرّفات من ١
   ⚠️ عشان الاختبار يبدأ من أرقام نظيفة: أول طلب يأخذ Id = 1، ورقم الطلب
      المولَّد يصير 2026-000001 (RegistrationService يبني الرقم من آخر رقم
      محفوظ، ومع خلوّ الجدول يبدأ من واحد).
   ⚠️ RESEED 0 لا 1: الدالة تضبط «آخر قيمة مستعملة»، فالصف التالي يأخذ ١.
   ============================================================================ */
DBCC CHECKIDENT ('Students',                 RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('Requests',                 RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('StudentDeclarations',      RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('WorkflowHistory',          RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('Notifications',            RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('StudentStatusActions',     RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('StudentStatusAttachments', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('HousingTransfers',         RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('AccountLifecycleLogs',     RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('BulkRequests',             RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('BulkRequestStudents',      RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('OTPVerifications',         RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('SMSLogs',                  RESEED, 0) WITH NO_INFOMSGS;

/* -------- بعد: تأكيد أن سكن هيئة التدريس لم يُمسّ -------- */
SELECT 'بعد الحذف' AS المرحلة,
       (SELECT COUNT(*) FROM Students)              AS الطلاب,
       (SELECT COUNT(*) FROM Requests)              AS الطلبات,
       (SELECT COUNT(*) FROM StudentDeclarations)   AS التعهدات,
       (SELECT COUNT(*) FROM StudentStatusActions)  AS إجراءات_الحالة,
       (SELECT COUNT(*) FROM FacultyUnits)          AS وحدات_هيئة_التدريس,
       (SELECT COUNT(*) FROM FacultyOccupancies)    AS إشغالات_هيئة_التدريس;

/* ============================================================================
   ⚠️ راجع الأرقام أعلاه قبل الاعتماد:
        • أعمدة الطلاب/الطلبات/التعهدات/إجراءات الحالة = 0
        • عمودا هيئة التدريس = نفس قيمتيهما قبل الحذف

      إن كان كل شيء كما هو متوقَّع نفّذ:   COMMIT TRANSACTION;
      وإن لم يكن كذلك نفّذ:                 ROLLBACK TRANSACTION;

      المعاملة مفتوحة عمدًا ولم تُغلق في السكربت - القرار بيدك بعد رؤية
      الأرقام، لا قبلها.
   ============================================================================ */

-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
