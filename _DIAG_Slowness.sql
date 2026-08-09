/* =====================================================================
   تشخيص بطء شاشات إعدادات النظام — قراءة فقط، لا يغيّر أي شيء.

   شغّله كله مرة واحدة في SSMS على NUH_DB وابعتلي صورة النتائج.
   ===================================================================== */
USE NUH_DB;
GO

/* (1) حجم الجداول التي تقرأ منها الشاشات البطيئة ------------------- */
SELECT  N'AuditLogs'   AS TableName, COUNT_BIG(*) AS Rows FROM AuditLogs
UNION ALL SELECT N'SignInLogs',      COUNT_BIG(*) FROM SignInLogs
UNION ALL SELECT N'ErrorLogs',       COUNT_BIG(*) FROM ErrorLogs
UNION ALL SELECT N'AuditChangeLogs', COUNT_BIG(*) FROM AuditChangeLogs
UNION ALL SELECT N'Users',           COUNT_BIG(*) FROM Users
UNION ALL SELECT N'Students',        COUNT_BIG(*) FROM Students
UNION ALL SELECT N'Requests',        COUNT_BIG(*) FROM Requests
ORDER BY Rows DESC;
GO

/* (2) المساحة التي تشغلها الجداول على القرص -------------------------- */
SELECT  t.name                        AS TableName,
        SUM(p.rows)                   AS [Rows],
        CAST(SUM(a.total_pages) * 8.0 / 1024 AS DECIMAL(10,1)) AS TotalMB
FROM    sys.tables t
JOIN    sys.indexes i      ON i.object_id = t.object_id
JOIN    sys.partitions p   ON p.object_id = t.object_id AND p.index_id = i.index_id
JOIN    sys.allocation_units a ON a.container_id = p.partition_id
WHERE   i.index_id IN (0,1)
GROUP BY t.name
ORDER BY TotalMB DESC;
GO

/* (3) الفهارس الموجودة على جداول السجلات ---------------------------- */
SELECT  OBJECT_NAME(i.object_id) AS TableName, i.name AS IndexName, i.type_desc,
        STUFF((SELECT ', ' + c.name
               FROM sys.index_columns ic
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
               ORDER BY ic.key_ordinal FOR XML PATH('')), 1, 2, '') AS Cols
FROM    sys.indexes i
WHERE   OBJECT_NAME(i.object_id) IN ('AuditLogs','SignInLogs','ErrorLogs','Users','AuditChangeLogs')
  AND   i.type_desc <> 'HEAP'
ORDER BY TableName, IndexName;
GO

/* (4) الزمن الفعلي لاستعلامات الشاشة — الأرقام الحاسمة --------------- */
SET STATISTICS TIME ON;

PRINT N'--- (أ) عدّاد الإجراءات (يُنفَّذ عند كل فتح للشاشة) ---';
SELECT action, COUNT(*) AS Cnt FROM AuditLogs GROUP BY action;

PRINT N'--- (ب) عدد المستخدمين المميّزين (يُنفَّذ عند كل فتح للشاشة) ---';
SELECT COUNT(DISTINCT user_id) FROM AuditLogs;

PRINT N'--- (ج) أول 50 صفًا مرتّبة بالتاريخ ---';
SELECT TOP 50 * FROM AuditLogs ORDER BY action_at DESC;

PRINT N'--- (د) قائمة فلتر المستخدمين ---';
SELECT Id, full_name, UserName FROM Users WHERE is_active = 1 ORDER BY full_name;

SET STATISTICS TIME OFF;
GO

/* (5) إصدار وإعدادات السيرفر ---------------------------------------- */
SELECT  SERVERPROPERTY('Edition')      AS Edition,
        SERVERPROPERTY('ProductLevel') AS ProductLevel,
        (SELECT value_in_use FROM sys.configurations WHERE name = 'max server memory (MB)') AS MaxMemoryMB,
        (SELECT COUNT(*) FROM sys.dm_os_schedulers WHERE status = 'VISIBLE ONLINE' AND is_online = 1) AS Schedulers;
GO
