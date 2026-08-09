/* =====================================================================
   منح صلاحية VIEW SERVER STATE لحساب خدمة النظام

   ليه:
     شاشة سجل الأخطاء بقت تسجّل الطلبات البطيئة، وبتحاول تفحص وقتها هل فيه
     استعلامات منتظرة ذاكرة في قاعدة البيانات (RESOURCE_SEMAPHORE) — وده
     السبب اللي عطّل الشاشات فعلًا. الفحص ده بيقرا من sys.dm_exec_* وبيحتاج
     الصلاحية دي، ومن غيرها بتكتب الرسالة صراحةً إنها متعذّرة.

   إيه اللي بتسمح بيه بالظبط:
     قراءة إحصاءات تشغيل السيرفر فقط — الجلسات، الانتظارات، منح الذاكرة،
     خطط التنفيذ. **ما بتديش أي وصول لبيانات إضافية ولا صلاحية تعديل.**
     وهي الصلاحية القياسية لأي حساب بيراقب أداء SQL Server.

   ⚠️ الصلاحية على مستوى السيرفر، فبتتنفّذ على master.
   ===================================================================== */

USE master;
GO

/* --- (1) الحالة الحالية: هل الصلاحية ممنوحة أصلًا؟ --------------------
   المتوقّع قبل التنفيذ: صفر صفوف. */
SELECT  pr.name        AS LoginName,
        pr.type_desc   AS LoginType,
        pe.permission_name,
        pe.state_desc  AS PermissionState
FROM    sys.server_permissions pe
JOIN    sys.server_principals  pr ON pr.principal_id = pe.grantee_principal_id
WHERE   pe.permission_name = 'VIEW SERVER STATE'
  AND   pr.name LIKE '%svc%';
GO

/* --- (2) تأكيد الاسم الصحيح للحساب ------------------------------------
   لو الحساب حساب ويندوز هيظهر باسم النطاق (مثلاً NUH\svc.nuh)،
   ولو حساب SQL هيظهر svc.nuh. خُد الاسم من هنا بالحرف. */
SELECT  name, type_desc, is_disabled, create_date
FROM    sys.server_principals
WHERE   name LIKE '%svc%' AND type IN ('S', 'U', 'G');
GO

/* --- (3) المنح -------------------------------------------------------
   ⚠️ عدّل الاسم لو طلع مختلف في القسم (2).
   السكربت بيمنح فقط لو الحساب موجود، فما بيفشلش لو الاسم اتغيّر. */
DECLARE @login sysname = N'svc.nuh';   -- ← غيّره لو القسم (2) طلع اسمًا آخر

IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @login)
BEGIN
    DECLARE @sql nvarchar(400) =
        N'GRANT VIEW SERVER STATE TO ' + QUOTENAME(@login) + N';';
    EXEC sp_executesql @sql;
    PRINT N'تم المنح للحساب: ' + @login;
END
ELSE
    PRINT N'⚠️ الحساب غير موجود بهذا الاسم — راجع مخرجات القسم (2): ' + @login;
GO

/* --- (4) تحقق بعد التنفيذ --------------------------------------------
   المتوقّع: صف واحد فيه VIEW SERVER STATE / GRANT. */
SELECT  pr.name AS LoginName, pe.permission_name, pe.state_desc AS PermissionState
FROM    sys.server_permissions pe
JOIN    sys.server_principals  pr ON pr.principal_id = pe.grantee_principal_id
WHERE   pe.permission_name = 'VIEW SERVER STATE'
  AND   pr.name LIKE '%svc%';
GO

/* --- (5) اختبار عملي: نفس الاستعلام اللي بيشغّله النظام ---------------
   المفروض يرجّع رقمًا (غالبًا 0) بدل رسالة رفض صلاحية. */
SELECT COUNT(*) AS QueriesWaitingForMemory
FROM   sys.dm_exec_query_memory_grants
WHERE  grant_time IS NULL;
GO

/* ---------------------------------------------------------------------
   للتراجع لو احتجت:
       USE master;
       REVOKE VIEW SERVER STATE FROM [svc.nuh];
   --------------------------------------------------------------------- */
