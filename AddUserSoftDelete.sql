/* ==================================================================
   NUH-PORTAL — أعمدة الحذف المنطقي للمستخدمين + مصدر الحساب
   ------------------------------------------------------------------
   شغّلها مرة واحدة على NUH_DB *قبل* نشر البناء الجديد.
   السكربت آمن للتكرار: كل خطوة بتتأكد الأول إن الحاجة مش موجودة.
   ================================================================== */
USE NUH_DB;
GO

/* ---------- 1) الأعمدة ---------- */
IF COL_LENGTH('dbo.Users', 'is_deleted') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD
        is_deleted  BIT          NOT NULL CONSTRAINT DF_Users_is_deleted DEFAULT (0),
        deleted_at  DATETIME2(7) NULL,
        deleted_by  INT          NULL,
        auth_source NVARCHAR(16) NULL;
    PRINT 'Users: added is_deleted / deleted_at / deleted_by / auth_source';
END
ELSE
    PRINT 'Users: columns already exist - skipped';
GO

/* ---------- 2) تعبئة مصدر الحساب للصفوف الحالية ----------
   الحساب اللي مالوش باسورد محلي بيدخل عبر الدليل (AD) — الباسورد عند
   الدومين مش عندنا. الباقي حسابات محلية اتعملت من جوّه النظام.        */
UPDATE dbo.Users
SET    auth_source = CASE WHEN PasswordHash IS NULL THEN 'ad' ELSE 'local' END
WHERE  auth_source IS NULL;
GO

/* ---------- 3) فهرس على is_deleted ----------
   كل قراءات شاشة المستخدمين بتفلتر بيه.                              */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Users_is_deleted'
                 AND object_id = OBJECT_ID('dbo.Users'))
BEGIN
    CREATE INDEX IX_Users_is_deleted ON dbo.Users (is_deleted);
    PRINT 'Users: created IX_Users_is_deleted';
END
GO

/* ---------- 4) تأكيد ---------- */
SELECT auth_source, COUNT(*) AS [count]
FROM   dbo.Users
GROUP  BY auth_source;

SELECT is_deleted, COUNT(*) AS [count]
FROM   dbo.Users
GROUP  BY is_deleted;
GO
