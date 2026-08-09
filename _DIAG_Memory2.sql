/* =====================================================================
   ذاكرة SQL Server — قراءة فقط. شغّله كله وابعت صورة كل الجداول.

   ليه تاني:
     سجل الأخطاء بيقول إن استعلامات بتنتهي مهلتها وهي مستنية ذاكرة
     (خطأ 8645 في resource pool 'internal'). ده مش «خطة استعلام سيّئة»
     — ده السيرفر كله مخنوق في الذاكرة. محتاج أعرف: عنده كام؟ وبياخد كام؟
   ===================================================================== */

/* (1) ذاكرة الجهاز ---------------------------------------------------
   ServerFreeMB القليلة + Low = الجهاز نفسه مخنوق (IIS + SQL على بعض). */
SELECT  total_physical_memory_kb/1024      AS ServerTotalMB,
        available_physical_memory_kb/1024  AS ServerFreeMB,
        system_memory_state_desc           AS ServerMemoryState
FROM    sys.dm_os_sys_memory;

/* (2) ذاكرة عملية SQL نفسها ------------------------------------------
   ProcessMemLow = 1 معناها ويندوز بيضغط على SQL ليسلّم ذاكرة. */
SELECT  physical_memory_in_use_kb/1024     AS SqlUsingMB,
        memory_utilization_percentage      AS SqlWorkingSetPct,
        process_physical_memory_low        AS ProcessMemLow,
        process_virtual_memory_low         AS ProcessVirtMemLow
FROM    sys.dm_os_process_memory;

/* (3) الحد المضبوط في الإعدادات --------------------------------------
   2147483647 = «بلا حد» (الافتراضي). على جهاز عليه IIS كمان، ده بيخلّي
   SQL وويندوز يتخانقوا على نفس الذاكرة. */
SELECT  name, value_in_use AS ValueMB
FROM    sys.configurations
WHERE   name IN ('max server memory (MB)', 'min server memory (MB)');

/* (4) حوض منح الذاكرة — الأعمدة الصحيحة (غلطت فيها المرة اللي فاتت) ---
   waiter_count > 0 = فيه استعلامات واقفة في الطابور دلوقتي.
   timeout_error_count = كام استعلام فشل بمهلة ذاكرة من آخر تشغيل. */
SELECT  resource_semaphore_id,
        pool_id,
        target_memory_kb/1024      AS TargetMB,
        max_target_memory_kb/1024  AS MaxTargetMB,
        total_memory_kb/1024       AS TotalMB,
        available_memory_kb/1024   AS AvailableMB,
        granted_memory_kb/1024     AS GrantedMB,
        used_memory_kb/1024        AS UsedMB,
        grantee_count, waiter_count, timeout_error_count, forced_grant_count
FROM    sys.dm_exec_query_resource_semaphores;

/* (5) أكثر عشر استعلامات طلبًا للذاكرة منذ آخر تشغيل ------------------
   لو فيه استعلام بيطلب مئات الميجابايت، هيبان هنا باسمه. */
SELECT TOP 10
        qs.max_grant_kb/1024       AS MaxGrantMB,
        qs.last_grant_kb/1024      AS LastGrantMB,
        qs.max_used_grant_kb/1024  AS MaxActuallyUsedMB,
        qs.execution_count         AS Runs,
        SUBSTRING(t.text, 1, 160)  AS SqlText
FROM    sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
ORDER BY qs.max_grant_kb DESC;
