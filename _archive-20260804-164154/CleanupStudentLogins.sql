/* ============================================================================
   حذف حسابات دخول الطلاب المتبقّية
   ----------------------------------------------------------------------------
   حساب الطالب بيتعمل تلقائيًا أول ما يتحقق برمز الجوال (دور "user").
   سكربت تنظيف بيانات التجربة مسح الطلاب والطلبات، لكن حسابات الدخول دي فضلت.

   ⚠️ الملف ده بيمسح المستخدمين اللي دورهم "user" فقط.
      الموظفين (admin / supervisor / cyber) مابيتمسّوش إطلاقًا.

   شغّل الخطوة ١ الأول واقرا الأسماء، وبعدين الخطوة ٢.
   ============================================================================ */

USE NUH_DB;
GO

/* ---------------- الخطوة ١: مين هيتمسح؟ (قراءة فقط) ---------------- */

PRINT '--- حسابات الطلاب اللي هتتمسح ---';
SELECT u.Id, u.UserName, u.full_name, u.Email, u.mobile, u.created_at
FROM dbo.Users u
INNER JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
INNER JOIN dbo.AspNetRoles r      ON r.Id      = ur.RoleId
WHERE r.Name = 'user'
ORDER BY u.created_at;

PRINT '--- الموظفون (هيفضلوا زي ما هم) ---';
SELECT u.Id, u.UserName, u.full_name, r.Name AS [الدور]
FROM dbo.Users u
INNER JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
INNER JOIN dbo.AspNetRoles r      ON r.Id      = ur.RoleId
WHERE r.Name <> 'user'
ORDER BY r.Name, u.UserName;

GO

/* ---------------- الخطوة ٢: الحذف ---------------- */
/*  ⚠️ ماتشغّلهاش غير بعد ما تراجع الأسماء فوق.
    كله جوّه معاملة واحدة: أي فشل بيرجّع كل حاجة زي ما كانت. */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY

    DECLARE @studentUsers TABLE (UserId int PRIMARY KEY);

    INSERT INTO @studentUsers (UserId)
    SELECT ur.UserId
    FROM dbo.AspNetUserRoles ur
    INNER JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
    WHERE r.Name = 'user';

    -- ⚠️ استبعاد أي مستخدم له دور تاني كمان — لو حساب اتسند له admin و user
    --    مثلاً، مايتمسحش. الأمان هنا أهم من اكتمال التنظيف.
    DELETE FROM @studentUsers
    WHERE UserId IN (
        SELECT ur.UserId
        FROM dbo.AspNetUserRoles ur
        INNER JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
        WHERE r.Name <> 'user'
    );

    -- ⚠️ PRINT مابيقبلش استعلام فرعي جوّه التعبير (Msg 1046)، والخطأ ده وقت
    --    التحويل البرمجي فالدفعة كلها مابتشتغلش أصلاً. بنحسب في متغيّر الأول.
    DECLARE @targetCount int = (SELECT COUNT(*) FROM @studentUsers);
    PRINT 'عدد حسابات الطلاب المستهدفة: ' + CAST(@targetCount AS varchar(10));

    -- جداول Identity المرتبطة
    DELETE FROM dbo.AspNetUserRoles  WHERE UserId IN (SELECT UserId FROM @studentUsers);
    PRINT 'AspNetUserRoles: '  + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.AspNetUserClaims WHERE UserId IN (SELECT UserId FROM @studentUsers);
    PRINT 'AspNetUserClaims: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.AspNetUserLogins WHERE UserId IN (SELECT UserId FROM @studentUsers);
    PRINT 'AspNetUserLogins: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.AspNetUserTokens WHERE UserId IN (SELECT UserId FROM @studentUsers);
    PRINT 'AspNetUserTokens: ' + CAST(@@ROWCOUNT AS varchar(10));

    DELETE FROM dbo.Users WHERE Id IN (SELECT UserId FROM @studentUsers);
    PRINT 'Users: ' + CAST(@@ROWCOUNT AS varchar(10));

    COMMIT TRANSACTION;
    PRINT '===== تم الحذف بنجاح =====';

END TRY
BEGIN CATCH

    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT '===== فشل — تم التراجع، ولم يُحذف أي صف =====';
    PRINT 'الرسالة: ' + ERROR_MESSAGE();
    THROW;

END CATCH
GO

/* ---------------- تحقق ---------------- */
SELECT r.Name AS [الدور], COUNT(*) AS [عدد المستخدمين]
FROM dbo.Users u
INNER JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
INNER JOIN dbo.AspNetRoles r      ON r.Id      = ur.RoleId
GROUP BY r.Name
ORDER BY r.Name;
GO
