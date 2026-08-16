/* ==================================================================
   NUH-PORTAL — تقسيم الطلاب / الطالبات على المشرفين
   ------------------------------------------------------------------
   شغّلها مرة واحدة على NUH_DB *قبل* نشر البناء الجديد.
   السكربت آمن للتكرار: كل خطوة بتتأكد الأول إن الحاجة مش موجودة.
   ================================================================== */
USE NUH_DB;
GO

/* ---------- 1) قسم الموظف: طلاب / طالبات / فاضي ----------
   فاضي = بلا تقييد. ومين يتخطّى التقييد بالكامل بيتحدد من صلاحية
   students.allGenders على الدور، مش من العمود ده.                    */
IF COL_LENGTH('dbo.Users', 'scope_gender') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD scope_gender NVARCHAR(10) NULL;
    PRINT 'Users: added scope_gender';
END
ELSE
    PRINT 'Users: scope_gender already exists - skipped';
GO

/* ---------- 2) جنس الطالب على صف الطلب ----------
   متكرّر عن قصد: طلب التسجيل الذاتي بيتقدّم و student_id فيه صفر لحد ما
   يتعتمد، وبيانات الطالب ساعتها نص JSON في RegistrationData — والـ JSON
   مايتفلترش في SQL. من غير العمود ده مافيش طريقة نوجّه الطلب المعلّق
   لمشرف قسمه، وهو بالظبط الطلب اللي محتاج التوجيه.                   */
IF COL_LENGTH('dbo.Requests', 'student_gender') IS NULL
BEGIN
    ALTER TABLE dbo.Requests ADD student_gender NVARCHAR(10) NULL;
    PRINT 'Requests: added student_gender';
END
ELSE
    PRINT 'Requests: student_gender already exists - skipped';
GO

/* ---------- 3) تعبئة جنس الطلبات الموجودة من سجل الطالب ---------- */
UPDATE r
SET    r.student_gender = s.gender
FROM   dbo.Requests r
JOIN   dbo.Students s ON s.Id = r.student_id
WHERE  r.student_gender IS NULL
  AND  s.gender IS NOT NULL;
PRINT CONCAT('Requests: backfilled ', @@ROWCOUNT, ' rows');
GO

/* ---------- 4) فهرس ----------
   كل استعلامات شاشة الطلبات بتفلتر بيه للمشرف المقيَّد.               */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Requests_student_gender'
                 AND object_id = OBJECT_ID('dbo.Requests'))
BEGIN
    CREATE INDEX IX_Requests_student_gender ON dbo.Requests (student_gender);
    PRINT 'Requests: created IX_Requests_student_gender';
END
GO

/* ==================================================================
   5) تحديد قسم كل مشرف  ⚠️  عدّل الأسماء دي قبل التشغيل
   ------------------------------------------------------------------
   سيبها فاضية لأي موظف عايزه يشوف الجنسين (هياخد الصلاحية من دوره).
   ================================================================== */
UPDATE dbo.Users SET scope_gender = 'male'   WHERE UserName = 'supervisor1';
UPDATE dbo.Users SET scope_gender = 'female' WHERE UserName = 'supervisor2';
GO

/* ---------- 6) تأكيد ---------- */
SELECT u.UserName, u.full_name, r.Name AS [role],
       ISNULL(u.scope_gender, N'(الجنسين)') AS [القسم]
FROM   dbo.Users u
LEFT   JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
LEFT   JOIN dbo.AspNetRoles r      ON r.Id = ur.RoleId
WHERE  u.is_deleted = 0
  AND  (r.Name IS NULL OR r.Name <> 'user')
ORDER  BY r.Name, u.UserName;

SELECT ISNULL(student_gender, N'(فاضي)') AS student_gender, COUNT(*) AS [count]
FROM   dbo.Requests
GROUP  BY student_gender;
GO
