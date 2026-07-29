-- ============================================================================
--  إسناد الأدوار للمستخدمين الموجودين — NUH_DB
-- ----------------------------------------------------------------------------
--  المشكلة: المستخدمين admin / supervisor1 / supervisor2 / cyber1 / cyber2
--  موجودين في جدول Users ومعاهم PasswordHash (يعني الدخول المحلي شغّال)،
--  لكن مالهمش أي صف في AspNetUserRoles — يعني بصفر أدوار وبالتالي صفر صلاحيات.
--
--  ⚠️ شغّل السكربت ده *بعد* ما التطبيق يقوم مرة واحدة بالتعديل الجديد،
--     لأن التطبيق هو اللي بينشئ الأدوار الأربعة وصلاحياتها في AspNetRoles.
--     لو شغّلته قبل كده مش هيسند حاجة (الأدوار لسه مش موجودة).
--
--  السكربت idempotent — تقدر تشغّله أكتر من مرة بأمان.
-- ============================================================================
USE NUH_DB;
GO

-- ─── قبل ───────────────────────────────────────────────────────────────────
PRINT '--- الوضع قبل الإسناد ---';
SELECT u.Id, u.UserName, r.Name AS RoleName
FROM dbo.Users u
LEFT JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN dbo.AspNetRoles     r  ON r.Id = ur.RoleId
ORDER BY u.Id;

-- ─── تحقّق أن الأدوار موجودة ────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE Name = 'admin')
BEGIN
    RAISERROR('دور admin غير موجود. شغّل التطبيق مرة واحدة أولاً ليُنشئ الأدوار وصلاحياتها، ثم أعد تشغيل هذا السكربت.', 16, 1);
    RETURN;
END
GO

-- ─── الإسناد ───────────────────────────────────────────────────────────────
--  admin        → admin
--  supervisor*  → supervisor      (supervisor1, supervisor2, supervisor)
--  cyber*       → cyber           (cyber1, cyber2, cyber)
--  أي مستخدم تاني (student_*) مش بيتلمس — دوره بيتحدد من مسار الـ OTP.
INSERT INTO dbo.AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM dbo.Users u
CROSS APPLY (
    SELECT CASE
        WHEN u.UserName = 'admin'          THEN 'admin'
        WHEN u.UserName LIKE 'supervisor%' THEN 'supervisor'
        WHEN u.UserName LIKE 'cyber%'      THEN 'cyber'
    END AS RoleName
) m
JOIN dbo.AspNetRoles r ON r.Name = m.RoleName
WHERE m.RoleName IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM dbo.AspNetUserRoles x
        WHERE x.UserId = u.Id AND x.RoleId = r.Id
  );

PRINT CONCAT('تم إسناد ', @@ROWCOUNT, ' دور.');
GO

-- ─── بعد ───────────────────────────────────────────────────────────────────
PRINT '--- الوضع بعد الإسناد ---';
SELECT u.Id, u.UserName, u.is_active,
       CASE WHEN u.PasswordHash IS NULL THEN 'AD only' ELSE 'local OK' END AS LocalLogin,
       r.Name AS RoleName
FROM dbo.Users u
LEFT JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN dbo.AspNetRoles     r  ON r.Id = ur.RoleId
ORDER BY u.Id;

-- عدد الصلاحيات لكل دور — لازم admin يطلع 16
SELECT r.Name AS RoleName, COUNT(rc.Id) AS PermissionCount
FROM dbo.AspNetRoles r
LEFT JOIN dbo.AspNetRoleClaims rc ON rc.RoleId = r.Id AND rc.ClaimType = 'permission'
GROUP BY r.Name
ORDER BY r.Name;
GO
