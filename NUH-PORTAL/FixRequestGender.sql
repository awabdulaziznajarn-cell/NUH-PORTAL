-- ============================================================================
--  تشخيص وإصلاح تقسيم الطلاب/الطالبات في جدول الطلبات
--  NUH-PORTAL  |  قاعدة NUH_DB
-- ============================================================================

-- ١) تشخيص: ما حال الطلب 2026-000014 ولماذا لم يظهر للمشرف؟
SELECT  r.request_number        AS [رقم الطلب],
        r.status                AS [الحالة],
        r.request_type          AS [النوع],
        r.student_gender        AS [جنس الطلب],
        s.gender                AS [جنس الطالب],
        s.full_name             AS [الطالب],
        s.student_id            AS [الرقم الجامعي],
        r.submitted_at          AS [وقت التقديم]
FROM        Requests r
LEFT JOIN   Students s ON s.Id = r.student_id
WHERE       r.request_number = '2026-000014';
GO

-- ٢) حجم المشكلة: كم طلبًا بلا جنس، وهل لطالبه جنس مسجّل؟
SELECT  CASE WHEN s.gender IS NULL THEN N'الطالب أيضًا بلا جنس - يحتاج تصحيحًا يدويًّا'
             ELSE N'يمكن تعبئته من سجل الطالب' END           AS [الحالة],
        COUNT(*)                                              AS [العدد]
FROM        Requests r
LEFT JOIN   Students s ON s.Id = r.student_id
WHERE       r.student_gender IS NULL
GROUP BY    CASE WHEN s.gender IS NULL THEN N'الطالب أيضًا بلا جنس - يحتاج تصحيحًا يدويًّا'
                 ELSE N'يمكن تعبئته من سجل الطالب' END;
GO

-- ٣) الإصلاح: تعبئة جنس الطلب من سجل الطالب.
--    ⚠️ لا يمسّ صفًّا له جنس بالفعل، ولا صفًّا طالبه بلا جنس.
BEGIN TRANSACTION;

UPDATE  r
SET     r.student_gender = s.gender
FROM    Requests r
JOIN    Students s ON s.Id = r.student_id
WHERE   r.student_gender IS NULL
  AND   s.gender IS NOT NULL;

PRINT N'عدد الصفوف المصحَّحة:';
PRINT @@ROWCOUNT;

-- راجع الرقم أعلاه. إن بدا صحيحًا:
COMMIT;
-- وإن لم يبدُ صحيحًا نفّذ بدلًا منه:  ROLLBACK;
GO

-- ٤) تحقّق بعد الإصلاح: يجب أن يكون العدد صفرًا أو يقتصر على طلاب بلا جنس
SELECT  COUNT(*) AS [طلبات لا تزال بلا جنس]
FROM    Requests
WHERE   student_gender IS NULL;
GO

-- ٥) الطلاب بلا جنس مسجّل - إن وُجدوا فصحّحهم من شاشة الطلاب
SELECT  s.student_id AS [الرقم الجامعي], s.full_name AS [الاسم], s.college AS [الكلية]
FROM    Students s
WHERE   s.gender IS NULL AND s.IsDeleted = 0;
GO
