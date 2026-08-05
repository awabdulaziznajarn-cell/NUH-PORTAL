-- ============================================================================
--  ضبط مسارات الأكتف دايركتوري — NUH_DB
-- ----------------------------------------------------------------------------
--  جدول dbo.ADConfiguration هو مصدر مسارات الـ OU والمجموعات اللي بيتعمل فيها
--  حساب الطالب. لو الصفوف دي ناقصة، الكود بيستخدم قيمة احتياطية — وde كان
--  السبب في إن الحسابات ما بتتعملش: القيمة الاحتياطية كانت لدومين قديم.
--
--  القيم هنا اتأخدت من الأكتف دايركتوري نفسه:
--      OU=MALE,OU=NEW,OU=STUDENTS,DC=nuh,DC=edu,DC=sa
--      OU=FEMALE,OU=NEW,OU=STUDENTS,DC=nuh,DC=edu,DC=sa
--      CN=NUH-Student-B,OU=GROUPS,DC=nuh,DC=edu,DC=sa
--      CN=NUH-Student-G,OU=GROUPS,DC=nuh,DC=edu,DC=sa
--
--  ملحوظة: student_ou_path بيتخزّن *بدون* OU=Male/OU=Female — الكود بيضيفها
--  تلقائيًا حسب جنس الطالب.
--
--  idempotent — يتشغّل أكتر من مرة من غير أي ضرر.
-- ============================================================================
USE NUH_DB;
GO

PRINT '--- قبل ---';
SELECT config_key, config_value FROM dbo.ADConfiguration ORDER BY config_key;
GO

DECLARE @rows TABLE (k NVARCHAR(200), v NVARCHAR(500), d NVARCHAR(500));

INSERT INTO @rows (k, v, d) VALUES
 ('student_ou_path',
  'OU=New,OU=Students,DC=nuh,DC=edu,DC=sa',
  N'الوحدة التنظيمية الأساسية لحسابات الطلاب — النظام يضيف OU=Male أو OU=Female تلقائيًا حسب الجنس'),
 ('male_group_dn',
  'CN=NUH-Student-B,OU=Groups,DC=nuh,DC=edu,DC=sa',
  N'مجموعة طلاب السكن (ذكور)'),
 ('female_group_dn',
  'CN=NUH-Student-G,OU=Groups,DC=nuh,DC=edu,DC=sa',
  N'مجموعة طالبات السكن (إناث)');

-- تحديث الموجود
UPDATE c
   SET c.config_value = r.v,
       c.description  = r.d,
       c.updated_at   = GETUTCDATE()
FROM dbo.ADConfiguration c
JOIN @rows r ON r.k = c.config_key
WHERE ISNULL(c.config_value, '') <> r.v;

PRINT CONCAT('تم تحديث ', @@ROWCOUNT, ' صف.');

-- إضافة الناقص
INSERT INTO dbo.ADConfiguration (config_key, config_value, description, updated_at)
SELECT r.k, r.v, r.d, GETUTCDATE()
FROM @rows r
WHERE NOT EXISTS (SELECT 1 FROM dbo.ADConfiguration c WHERE c.config_key = r.k);

PRINT CONCAT('تمت إضافة ', @@ROWCOUNT, ' صف.');
GO

PRINT '--- بعد ---';
SELECT config_key, config_value, updated_at
FROM dbo.ADConfiguration
WHERE config_key IN ('student_ou_path', 'male_group_dn', 'female_group_dn')
ORDER BY config_key;
GO
