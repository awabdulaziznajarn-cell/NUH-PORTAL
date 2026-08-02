-- ============================================================================
--  ترقيم الطلبات اللي فضلت بدون رقم — NUH_DB
-- ----------------------------------------------------------------------------
--  BackfillRequestNumbers.sql بيدي الطلب رقمًا مبنيًا على رقم الصف (سنة + Id)،
--  عشان يطابق الرقم اللي الشاشة كانت بتعرضه. لكن لو الرقم ده محجوز بالفعل
--  لطلب تاني، السكربت بيتخطّاه بدل ما يكسر الفهرس الفريد على request_number.
--
--  السكربت ده بياخد اللي فضل ويديه **أول رقم فاضي في التسلسل** (max + 1)،
--  فمفيش أي احتمال تصادم.
--
--  idempotent — لو مفيش صفوف بدون رقم، مش هيعمل حاجة.
-- ============================================================================
USE NUH_DB;
GO

PRINT '--- قبل: طلبات بدون رقم ---';
SELECT Id, request_type, status, submitted_at
FROM dbo.Requests
WHERE request_number IS NULL
ORDER BY Id;
GO

;WITH todo AS (
    SELECT Id,
           CAST(YEAR(ISNULL(submitted_at, GETUTCDATE())) AS VARCHAR(4)) AS yr,
           ROW_NUMBER() OVER (
               PARTITION BY YEAR(ISNULL(submitted_at, GETUTCDATE()))
               ORDER BY submitted_at, Id) AS rn
    FROM dbo.Requests
    WHERE request_number IS NULL
),
maxes AS (
    -- أعلى تسلسل مستخدم لكل سنة. الصيغة 2026-000001 = 11 حرفًا.
    SELECT LEFT(request_number, 4) AS yr,
           MAX(TRY_CAST(RIGHT(request_number, 6) AS INT)) AS maxSeq
    FROM dbo.Requests
    WHERE request_number IS NOT NULL AND LEN(request_number) = 11
    GROUP BY LEFT(request_number, 4)
)
UPDATE r
   SET request_number = t.yr + '-' +
       RIGHT('000000' + CAST(ISNULL(m.maxSeq, 0) + t.rn AS VARCHAR(10)), 6)
FROM dbo.Requests r
JOIN todo  t ON t.Id = r.Id
LEFT JOIN maxes m ON m.yr = t.yr;

PRINT CONCAT('تم ترقيم ', @@ROWCOUNT, ' طلب.');
GO

-- ─── بعد ───────────────────────────────────────────────────────────────────
PRINT '--- كل الطلبات ---';
SELECT r.Id, r.request_number, r.request_type, r.status,
       s.full_name, s.phone, RIGHT(ISNULL(s.phone, ''), 4) AS Last4
FROM dbo.Requests r
LEFT JOIN dbo.Students s ON s.Id = r.student_id
ORDER BY r.Id;

PRINT '--- المفروض تكون فاضية: طلبات لسه بدون رقم ---';
SELECT Id, request_type, status FROM dbo.Requests WHERE request_number IS NULL;

PRINT '--- المفروض تكون فاضية: أرقام مكررة ---';
SELECT request_number, COUNT(*) AS Cnt
FROM dbo.Requests
WHERE request_number IS NOT NULL
GROUP BY request_number
HAVING COUNT(*) > 1;
GO
