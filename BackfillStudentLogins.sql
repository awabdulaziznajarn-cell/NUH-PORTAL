/* =====================================================================
   توحيد صيغة حسابات دخول الطلاب — تشغيل لمرة واحدة

   لماذا:
     الحساب يُنشأ لحظة تحقق الطالب برمز جواله، أي قبل وجود سجل طالب له.
     فيولد باسم مؤقت مبني على رقم الجوال واسمه «طالب». الكود الجديد يرقّيه
     تلقائيًا فور تقديم الطلب، لكن الحسابات المنشأة قبل هذا التعديل بقيت
     على شكلها القديم — ومنها يأتي اختلاف الصيغتين في الشاشة:

        student_966533333322   ←  مؤقت (رقم جوال)
        student_456336111      ←  نهائي (رقم جامعي)

   ما يفعله:
     يربط كل حساب طالب بسجل الطالب عبر رقم الجوال، ثم يضبط اسم المستخدم
     على student_<الرقم الجامعي> والاسم الكامل على اسم الطالب الحقيقي.
     الحسابات التي لا سجل طالب لها تُترك كما هي — سترقّى تلقائيًا أول ما
     يقدّم صاحبها طلبًا.

   ⚠️ اقرأ أولًا: نفّذ القسم (1) وحده وراجع المخرجات قبل تشغيل القسم (2).
   ⚠️ خذ نسخة احتياطية من قاعدة البيانات قبل التنفيذ.
   ===================================================================== */

USE NUH_DB;
GO

/* ---------------------------------------------------------------------
   (1) معاينة — لا يغيّر شيئًا. شغّل هذا وحده أولًا.
   --------------------------------------------------------------------- */
;WITH m AS (
    SELECT  u.Id,
            u.UserName                AS CurrentUserName,
            u.full_name               AS CurrentFullName,
            u.mobile,
            s.student_id,
            s.full_name               AS StudentName,
            'student_' + s.student_id AS NewUserName
    FROM    Users   u
    JOIN    Students s
              ON  REPLACE(REPLACE(REPLACE(s.phone, '+', ''), ' ', ''), '-', '')
                = REPLACE(REPLACE(REPLACE(u.mobile, '+', ''), ' ', ''), '-', '')
    WHERE   u.UserName LIKE 'student[_]%'
)
SELECT  Id, CurrentUserName, NewUserName, CurrentFullName, StudentName, mobile,
        CASE WHEN CurrentUserName <> NewUserName THEN 'اسم المستخدم' ELSE '' END
      + CASE WHEN ISNULL(CurrentFullName, N'') <> ISNULL(StudentName, N'')
             AND StudentName IS NOT NULL THEN N' + الاسم الكامل' ELSE '' END AS WillChange
FROM    m
WHERE   CurrentUserName <> NewUserName
   OR  (StudentName IS NOT NULL AND ISNULL(CurrentFullName, N'') <> StudentName)
ORDER BY CurrentUserName;
GO

/* تحقق من عدم وجود تعارض: رقمان جامعيان مختلفان يولّدان نفس الاسم، أو
   اسم مستخدم محجوز بالفعل لحساب آخر. المتوقّع: صفر صفوف. */
SELECT  'student_' + s.student_id AS NewUserName, COUNT(*) AS Cnt
FROM    Users u
JOIN    Students s
          ON  REPLACE(REPLACE(REPLACE(s.phone, '+', ''), ' ', ''), '-', '')
            = REPLACE(REPLACE(REPLACE(u.mobile, '+', ''), ' ', ''), '-', '')
WHERE   u.UserName LIKE 'student[_]%'
GROUP BY 'student_' + s.student_id
HAVING  COUNT(*) > 1;
GO

/* ---------------------------------------------------------------------
   (2) التنفيذ — لا تشغّله إلا بعد مراجعة مخرجات القسم (1).
       داخل معاملة: راجع عدد الصفوف ثم COMMIT أو ROLLBACK.

   NormalizedUserName يُحدَّث معه — ASP.NET Identity يبحث به لا بـ UserName،
   وتركُه قديمًا يعني حسابًا لا يُعثر عليه بالاسم الجديد.
   --------------------------------------------------------------------- */
BEGIN TRAN;

UPDATE  u
SET     u.UserName           = 'student_' + s.student_id,
        u.NormalizedUserName = UPPER('student_' + s.student_id),
        u.full_name          = CASE WHEN LTRIM(RTRIM(ISNULL(s.full_name, N''))) <> N''
                                    THEN s.full_name ELSE u.full_name END
FROM    Users u
JOIN    Students s
          ON  REPLACE(REPLACE(REPLACE(s.phone, '+', ''), ' ', ''), '-', '')
            = REPLACE(REPLACE(REPLACE(u.mobile, '+', ''), ' ', ''), '-', '')
WHERE   u.UserName LIKE 'student[_]%'
  AND   u.UserName <> 'student_' + s.student_id
  AND   NOT EXISTS (SELECT 1 FROM Users x
                    WHERE x.UserName = 'student_' + s.student_id AND x.Id <> u.Id);

PRINT N'عدد الحسابات التي تغيّر اسمها:';
PRINT @@ROWCOUNT;

/* الاسم الكامل للحسابات التي اسمها صحيح أصلًا لكنها ما زالت «طالب» */
UPDATE  u
SET     u.full_name = s.full_name
FROM    Users u
JOIN    Students s
          ON  REPLACE(REPLACE(REPLACE(s.phone, '+', ''), ' ', ''), '-', '')
            = REPLACE(REPLACE(REPLACE(u.mobile, '+', ''), ' ', ''), '-', '')
WHERE   u.UserName LIKE 'student[_]%'
  AND   LTRIM(RTRIM(ISNULL(s.full_name, N''))) <> N''
  AND   ISNULL(u.full_name, N'') <> s.full_name;

PRINT N'عدد الحسابات التي تغيّر اسمها الكامل:';
PRINT @@ROWCOUNT;

-- راجع الأرقام أعلاه، ثم نفّذ أحد السطرين:
-- COMMIT TRAN;
-- ROLLBACK TRAN;
GO

/* ---------------------------------------------------------------------
   (3) تحقق بعد الـ COMMIT — المتوقّع: كل الأسماء بصيغة الرقم الجامعي،
       وما بقي بصيغة الجوال هو فقط من لا سجل طالب له بعد.
   --------------------------------------------------------------------- */
SELECT  u.UserName, u.full_name, u.mobile, s.student_id,
        CASE WHEN s.student_id IS NULL THEN N'مؤقت — لا سجل طالب بعد'
             WHEN u.UserName = 'student_' + s.student_id THEN N'موحّد'
             ELSE N'يحتاج مراجعة' END AS Status
FROM    Users u
LEFT JOIN Students s
          ON  REPLACE(REPLACE(REPLACE(s.phone, '+', ''), ' ', ''), '-', '')
            = REPLACE(REPLACE(REPLACE(u.mobile, '+', ''), ' ', ''), '-', '')
WHERE   u.UserName LIKE 'student[_]%'
ORDER BY Status, u.UserName;
GO
