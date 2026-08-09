/* =====================================================================
   منح الذاكرة في SQL Server — شغّله **والشاشة واقفة**.
   قراءة فقط.
   ===================================================================== */
USE NUH_DB;
GO

/* (1) مين ماسك الذاكرة ومين مستني -------------------------------------
   requested_memory_kb الكبير على قاعدة صغيرة = تقدير خاطئ للخطة.
   grant_time = NULL يعني لسه في الطابور (RESOURCE_SEMAPHORE). */
SELECT  g.session_id,
        g.requested_memory_kb / 1024.0 AS RequestedMB,
        g.granted_memory_kb  / 1024.0 AS GrantedMB,
        g.ideal_memory_kb    / 1024.0 AS IdealMB,
        g.required_memory_kb / 1024.0 AS RequiredMB,
        g.queue_id, g.wait_order, g.wait_time_ms, g.is_next_candidate,
        g.grant_time,
        SUBSTRING(t.text, 1, 200) AS SqlText
FROM    sys.dm_exec_query_memory_grants g
OUTER APPLY sys.dm_exec_sql_text(g.sql_handle) t
ORDER BY g.requested_memory_kb DESC;
GO

/* (2) حجم حوض المنح المتاح فعلًا ------------------------------------- */
SELECT  pool_id, name,
        max_memory_kb            / 1024.0 AS MaxPoolMB,
        used_memory_kb           / 1024.0 AS UsedMB,
        max_query_grant_memory_kb/ 1024.0 AS MaxSingleQueryGrantMB,
        available_memory_kb      / 1024.0 AS AvailableMB
FROM    sys.dm_exec_query_resource_semaphores;
GO

/* (3) ذاكرة العملية والجهاز ------------------------------------------- */
SELECT  physical_memory_in_use_kb/1024.0 AS SqlUsingMB,
        large_page_allocations_kb/1024.0 AS LargePagesMB,
        memory_utilization_percentage    AS UtilPct,
        process_physical_memory_low      AS ProcessMemLow,
        process_virtual_memory_low       AS ProcessVirtMemLow
FROM    sys.dm_os_process_memory;

SELECT  total_physical_memory_kb/1024.0     AS ServerTotalMB,
        available_physical_memory_kb/1024.0 AS ServerFreeMB,
        system_memory_state_desc            AS ServerMemoryState
FROM    sys.dm_os_sys_memory;
GO

/* (4) أكثر الاستعلامات طلبًا للذاكرة منذ آخر تشغيل -------------------- */
SELECT TOP 10
        qs.max_grant_kb/1024.0  AS MaxGrantMB,
        qs.last_grant_kb/1024.0 AS LastGrantMB,
        qs.max_used_grant_kb/1024.0 AS MaxUsedMB,
        qs.execution_count,
        SUBSTRING(t.text, 1, 200) AS SqlText
FROM    sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
ORDER BY qs.max_grant_kb DESC;
GO
