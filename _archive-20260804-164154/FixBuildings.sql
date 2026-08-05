-- ============================================================================
--  تصحيح نوع المباني (بنين/بنات) — NUH_DB
-- ----------------------------------------------------------------------------
--  التوزيع المعتمد:   65 → 70 = بنين      |      40 → 43 = بنات
--
--  الوضع الغلط اللي كان مزروع في الداتابيز:
--      40, 41, 42, 43  = male     ← مقلوب
--      65, 66, 67      = male
--      68, 69, 70      = female   ← مقلوب
--
--  شاشة تسجيل الطالب اتظبطت على التوزيع الصحيح، لكن جدول Buildings لأ —
--  فأي شاشة بتقرأ المباني من الداتابيز (إدارة المباني، تعديل بيانات الطالب،
--  التقارير) كانت بتوريه مقلوب.
--
--  Gender بيتخزّن نص 'male' / 'female' (GenderConverter) — مش رقم.
--  السكربت idempotent — تقدر تشغّله أكتر من مرة بأمان.
-- ============================================================================
USE NUH_DB;
GO

-- ─── قبل ───────────────────────────────────────────────────────────────────
PRINT '--- المباني قبل التصحيح ---';
SELECT Code, ArName, Gender, IsActive FROM dbo.Buildings ORDER BY TRY_CAST(Code AS INT), Code;
GO

-- ─── التصحيح ───────────────────────────────────────────────────────────────
UPDATE dbo.Buildings
   SET Gender = 'male'
 WHERE Code IN ('65','66','67','68','69','70')
   AND (Gender IS NULL OR Gender <> 'male');
PRINT CONCAT('مباني البنين المصحّحة: ', @@ROWCOUNT);

UPDATE dbo.Buildings
   SET Gender = 'female'
 WHERE Code IN ('40','41','42','43')
   AND (Gender IS NULL OR Gender <> 'female');
PRINT CONCAT('مباني البنات المصحّحة: ', @@ROWCOUNT);
GO

-- ─── بعد ───────────────────────────────────────────────────────────────────
PRINT '--- المباني بعد التصحيح ---';
SELECT Code, ArName, Gender, IsActive FROM dbo.Buildings ORDER BY TRY_CAST(Code AS INT), Code;

-- أي مبنى تاني مش في القائمتين (اتضاف يدويًا من شاشة إدارة المباني) —
-- السكربت مش بيلمسه، بس بيوريهولك عشان تراجعه بنفسك.
PRINT '--- مباني خارج التوزيع المعتمد (لم تُلمس) ---';
SELECT Code, ArName, Gender FROM dbo.Buildings
WHERE Code NOT IN ('40','41','42','43','65','66','67','68','69','70')
ORDER BY TRY_CAST(Code AS INT), Code;
GO

-- ─── فحص أثر جانبي ─────────────────────────────────────────────────────────
--  الطلاب اللي كانوا متسكّنين حسب التوزيع القديم بقوا في مبنى نوعه مختلف عن
--  جنسهم. السكربت *مش* بينقل حد — بس بيطلعلك القائمة عشان الإسكان يراجعها.
PRINT '--- طلاب في مبنى لا يطابق جنسهم (للمراجعة فقط — لم يُنقل أحد) ---';
SELECT s.student_id, s.full_name, s.gender AS StudentGender,
       s.housing_building AS BuildingCode, b.Gender AS BuildingGender
FROM dbo.Students s
JOIN dbo.Buildings b ON b.Code = s.housing_building
WHERE s.IsDeleted = 0
  AND b.Gender IS NOT NULL
  AND s.gender <> b.Gender
ORDER BY TRY_CAST(s.housing_building AS INT), s.student_id;
GO
