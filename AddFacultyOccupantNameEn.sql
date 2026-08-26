-- ==================================================================
--  NUH-PORTAL - سكن اعضاء هيئة التدريس  [2/2] الاسم الانجليزي واسم الدخول
-- ------------------------------------------------------------------
--  بيضيف عمودين على الجداول الموجودة - مافيش انشاء ولا مسح ولا تعديل
--  على اي بيانات مسجلة:
--
--    FacultyOccupancies.FullNameEn        الاسم الانجليزي للساكن
--                                         مصدره ووجهته displayName في الدليل
--
--    FacultyUnits.AdUserPrincipalName     اسم الدخول الكامل للوحدة
--                                         بيتقرا من الدليل ومابيتكتبش فيه
--
--  الملف امن للتشغيل اكتر من مرة: كل حاجة جوه IF NOT EXISTS.
--
--  ⚠️ العمودان NULL عن قصد. الوحدات المسجلة قبل التغيير ده مالهاش اسم
--     انجليزي ولا اسم دخول محفوظ، وNOT NULL بقيمة افتراضية كان معناه
--     صف بيقول "الاسم الانجليزي فاضي" بدل "الاسم الانجليزي مش متسجل"
--     - والاتنين مش نفس الحاجة عند المراجعة.
--
--  ملاحظة: كل التعليقات بشرطتين عن قصد - زي AddFacultyHousing.sql.
-- ==================================================================
USE NUH_DB;
GO

SET NOCOUNT ON;
GO

-- ------------------------------------------------------------------
-- 1) الاسم الانجليزي على سجل الاشغال
-- ------------------------------------------------------------------
IF COL_LENGTH('dbo.FacultyOccupancies', 'FullNameEn') IS NULL
BEGIN
    ALTER TABLE dbo.FacultyOccupancies ADD FullNameEn NVARCHAR(250) NULL;
    PRINT 'FacultyOccupancies.FullNameEn added.';
END
ELSE
    PRINT 'FacultyOccupancies.FullNameEn already exists - skipped.';
GO

-- ------------------------------------------------------------------
-- 2) اسم الدخول الكامل على الوحدة
-- ------------------------------------------------------------------
-- ⚠️ 256 مش 64: الـ UPN هو اسم الحساب + @ + اسم الدومين، فمقاسه اكبر من
--    AdAccount حتما. و256 هو الحد اللي الدليل نفسه بيقف عنده.
IF COL_LENGTH('dbo.FacultyUnits', 'AdUserPrincipalName') IS NULL
BEGIN
    ALTER TABLE dbo.FacultyUnits ADD AdUserPrincipalName NVARCHAR(256) NULL;
    PRINT 'FacultyUnits.AdUserPrincipalName added.';
END
ELSE
    PRINT 'FacultyUnits.AdUserPrincipalName already exists - skipped.';
GO

-- ------------------------------------------------------------------
-- 3) تأكيد
-- ------------------------------------------------------------------
-- ⚠️ العمودان بيفضلوا فاضيين لحد اول "مزامنة مع الدليل" من الشاشة:
--    الاستيراد هو اللي بيقرا displayName و userPrincipalName ويملاهم.
--    السكربت بينشئ المكان بس، مابينقلش بيانات.
SELECT
    (SELECT COUNT(*) FROM sys.columns
      WHERE object_id = OBJECT_ID('dbo.FacultyOccupancies') AND name = 'FullNameEn')          AS [عمود الاسم الانجليزي],
    (SELECT COUNT(*) FROM sys.columns
      WHERE object_id = OBJECT_ID('dbo.FacultyUnits') AND name = 'AdUserPrincipalName')       AS [عمود اسم الدخول];

SELECT
    COUNT(*)                                              AS [صفوف الاشغال],
    SUM(CASE WHEN FullNameEn IS NULL THEN 1 ELSE 0 END)   AS [بلا اسم انجليزي]
FROM dbo.FacultyOccupancies;
GO

PRINT 'Done. Run an AD import from the units screen to fill both columns.';
GO
