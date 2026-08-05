-- ============================================================================
--  تعبئة أرقام الطلبات الناقصة — NUH_DB
-- ----------------------------------------------------------------------------
--  الطلبات اللي بيعملها موظف (request_type = 'housing') كانت بتتخزّن
--  بـ request_number = NULL، لأن توليد الرقم كان في مسار تسجيل الطالب بس.
--
--  النتيجة: شاشة الطلبات بتعرض رقمًا محسوبًا وقت العرض من رقم الصف
--  (2026-000009 مثلًا)، والطالب يكتبه في صفحة التتبع فمايتلاقاش — لأنه
--  ماكانش متخزّن في الأساس.
--
--  الكود اتظبط (RequestService.CreateAsync بقى يولّد الرقم)، والسكربت ده
--  للصفوف القديمة بس. بيستخدم نفس الرقم اللي الشاشة بتعرضه حاليًا
--  (السنة + رقم الصف) عشان اللي اتكتب في ورق أو اتقال لطالب يفضل صالح.
--
--  idempotent — بيلمس الصفوف اللي رقمها NULL بس، وبيتخطّى أي رقم متكرر.
-- ============================================================================
USE NUH_DB;
GO

-- ─── قبل ───────────────────────────────────────────────────────────────────
PRINT '--- طلبات بدون رقم ---';
SELECT r.Id, r.request_number, r.request_type, r.status, s.full_name, s.phone
FROM dbo.Requests r
LEFT JOIN dbo.Students s ON s.Id = r.student_id
WHERE r.request_number IS NULL
ORDER BY r.Id;
GO

-- ─── التعبئة ───────────────────────────────────────────────────────────────
--  الرقم = سنة التقديم + رقم الصف بستة خانات — نفس اللي الواجهة بتعرضه.
--  NOT EXISTS بيمنع أي تصادم مع رقم متخزّن بالفعل (الفهرس فريد).
UPDATE r
   SET request_number = t.NewNumber
FROM dbo.Requests r
CROSS APPLY (
    SELECT CONCAT(YEAR(ISNULL(r.submitted_at, GETUTCDATE())), '-',
                  RIGHT(CONCAT('000000', CAST(r.Id AS VARCHAR(10))), 6)) AS NewNumber
) t
WHERE r.request_number IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Requests x WHERE x.request_number = t.NewNumber);

PRINT CONCAT('تم ترقيم ', @@ROWCOUNT, ' طلب.');
GO

-- ─── بعد ───────────────────────────────────────────────────────────────────
PRINT '--- كل الطلبات بعد الترقيم ---';
SELECT r.Id, r.request_number, r.request_type, r.status,
       s.full_name, s.phone, RIGHT(ISNULL(s.phone, ''), 4) AS Last4
FROM dbo.Requests r
LEFT JOIN dbo.Students s ON s.Id = r.student_id
ORDER BY r.Id;

-- لو فضل أي صف بدون رقم، معناه إن الرقم المحسوب متعارض مع رقم موجود.
-- راجعه يدويًا بدل ما السكربت يخمّن.
PRINT '--- طلبات لسه بدون رقم (تحتاج مراجعة يدوية) ---';
SELECT Id, request_type, status, submitted_at
FROM dbo.Requests
WHERE request_number IS NULL;

-- طلاب بدون رقم جوال — دول مايقدروش يتابعوا طلبهم (التحقق بآخر ٤ أرقام)
-- ولا يستقبلوا رسائل. رقم الجوال بقى مطلوب في شاشة التسجيل، لكن دي بيانات قديمة.
PRINT '--- طلبات لطلاب بدون رقم جوال (لن يتمكنوا من التتبع) ---';
SELECT r.request_number, s.student_id, s.full_name
FROM dbo.Requests r
JOIN dbo.Students s ON s.Id = r.student_id
WHERE s.phone IS NULL OR LTRIM(RTRIM(s.phone)) = '';
GO
