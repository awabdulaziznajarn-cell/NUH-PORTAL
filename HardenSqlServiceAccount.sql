/* =====================================================================
   تقليل صلاحيات حساب خدمة النظام إلى أقل ما يكفيه

   الوضع الحالي (متحقَّق منه):
       svc.nuh  →  sysadmin + dbcreator

   المطلوب:
       svc.nuh  →  db_owner على NUH_DB فقط  +  VIEW SERVER STATE

   ⚠️ لا تشغّله في وقت عمل. الخطوات تُنفَّذ بالترتيب، والقسم (5) يوقف
      كل شيء إن لم يوجد حساب sysadmin آخر — حتى لا تفقد التحكم بالسيرفر.

   ⚠️ شغّله بحساب sysadmin **غير** svc.nuh (حساب ويندوز Administrator مثلًا)،
      وإلا فقد تسحب صلاحيتك أثناء التنفيذ.
   ===================================================================== */

USE master;
GO

/* --- (1) من أنا الآن؟ يجب ألا يكون svc.nuh -------------------------- */
SELECT  SUSER_NAME()                          AS RunningAs,
        IS_SRVROLEMEMBER('sysadmin')          AS AmISysadmin;   -- يجب أن يكون 1
GO

/* --- (2) الوضع الحالي للحساب ---------------------------------------- */
SELECT  r.name AS ServerRole
FROM    sys.server_role_members m
JOIN    sys.server_principals r ON r.principal_id = m.role_principal_id
JOIN    sys.server_principals p ON p.principal_id = m.member_principal_id
WHERE   p.name = 'svc.nuh';
GO

/* --- (3) هل يوجد sysadmin آخر مفعّل؟ (شرط السلامة) ------------------
   المتوقّع: صف واحد على الأقل غير svc.nuh. */
SELECT  p.name, p.type_desc, p.is_disabled
FROM    sys.server_role_members m
JOIN    sys.server_principals r ON r.principal_id = m.role_principal_id
JOIN    sys.server_principals p ON p.principal_id = m.member_principal_id
WHERE   r.name = 'sysadmin' AND p.name <> 'svc.nuh' AND p.is_disabled = 0;
GO

/* --- (4) امنح البديل أولًا — قبل سحب أي شيء -------------------------
   الترتيب مقصود: نضيف الصلاحية المطلوبة ثم نسحب الزائدة. العكس يعني
   نافذة زمنية يكون فيها التطبيق بلا صلاحية كافية. */
USE NUH_DB;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'svc.nuh')
    CREATE USER [svc.nuh] FOR LOGIN [svc.nuh];
GO
ALTER ROLE db_owner ADD MEMBER [svc.nuh];
GO

USE master;
GO
GRANT VIEW SERVER STATE TO [svc.nuh];   -- لتشخيص البطء في سجل الأخطاء
GO

/* --- (5) السحب — لا ينفّذ إلا إذا وُجد sysadmin آخر مفعّل ----------- */
DECLARE @otherSysadmins int =
(
    SELECT COUNT(*)
    FROM   sys.server_role_members m
    JOIN   sys.server_principals r ON r.principal_id = m.role_principal_id
    JOIN   sys.server_principals p ON p.principal_id = m.member_principal_id
    WHERE  r.name = 'sysadmin' AND p.name <> 'svc.nuh' AND p.is_disabled = 0
);

IF @otherSysadmins = 0
BEGIN
    RAISERROR (N'توقّف: لا يوجد حساب sysadmin آخر مفعّل. سحب الصلاحية الآن يعني فقدان التحكم بالسيرفر. أضف حسابًا إداريًا أولًا.', 16, 1);
END
ELSE
BEGIN
    ALTER SERVER ROLE sysadmin  DROP MEMBER [svc.nuh];
    ALTER SERVER ROLE dbcreator DROP MEMBER [svc.nuh];
    PRINT N'تم سحب sysadmin و dbcreator من svc.nuh.';
END
GO

/* --- (6) تحقق نهائي --------------------------------------------------
   المتوقّع: لا أدوار خادم، وdb_owner على NUH_DB، وVIEW SERVER STATE ممنوحة. */
SELECT  r.name AS RemainingServerRole
FROM    sys.server_role_members m
JOIN    sys.server_principals r ON r.principal_id = m.role_principal_id
JOIN    sys.server_principals p ON p.principal_id = m.member_principal_id
WHERE   p.name = 'svc.nuh';

SELECT  pe.permission_name, pe.state_desc
FROM    sys.server_permissions pe
JOIN    sys.server_principals pr ON pr.principal_id = pe.grantee_principal_id
WHERE   pr.name = 'svc.nuh';
GO

USE NUH_DB;
GO
SELECT  dp.name AS DatabaseRole
FROM    sys.database_role_members m
JOIN    sys.database_principals dp ON dp.principal_id = m.role_principal_id
JOIN    sys.database_principals u  ON u.principal_id  = m.member_principal_id
WHERE   u.name = 'svc.nuh';
GO

/* --- (7) بعد التنفيذ: اختبر النظام فورًا -----------------------------
       • افتح الشاشات (طلاب / طلبات / سجل العمليات)
       • سجّل دخول وخروج
       • جرّب تسجيل طالب جديد
       • لو ظهر خطأ صلاحية في سجل الأخطاء، راجع القسم (8)
   --------------------------------------------------------------------- */

/* --- (8) التراجع الفوري لو حصلت مشكلة -------------------------------
       USE master;
       ALTER SERVER ROLE sysadmin ADD MEMBER [svc.nuh];
   --------------------------------------------------------------------- */
