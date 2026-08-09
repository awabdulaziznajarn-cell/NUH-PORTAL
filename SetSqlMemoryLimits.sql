/* =====================================================================
   ضبط حدود ذاكرة SQL Server — الإصلاح الفعلي لتوقّف الشاشات

   المشكلة المرصودة:
       min server memory =         16 MB
       max server memory = 2147483647 MB   (بلا حد)
       SQL يستهلك فعليًا  =        133 MB  من 6143 MB

     بلا أرضية، ينكمش SQL تحت ضغط ويندوز حتى يقارب 16 ميجابايت. وعندها
     حتى استعلام يطلب ميجابايت واحدًا لا يجده، فينتظر في طابور الذاكرة
     (RESOURCE_SEMAPHORE) عشرات الثواني، ثم يأخذ منحة قسرية أو تنتهي مهلته
     بالخطأ 8645. وهذا ما ظهر: 26 انتهاء مهلة و288 منحة قسرية، مع أن أكبر
     منحة طلبها أي استعلام في النظام ميجابايت واحد فقط.

   القيم المختارة (جهاز 6 جيجابايت عليه IIS + التطبيق + SQL):
       min = 1024 MB   أرضية لا ينزل تحتها مهما ضغط ويندوز
       max = 1536 MB   سقف يترك ~4.5 جيجابايت لويندوز و IIS والتطبيق

     ⚠️ SQL Server Express يحدّ مجمّع الذاكرة عنده بنحو 1410 ميجابايت
        أصلًا، فالسقف 1536 لا يمنحه أكثر من حقه — لكنه يمنع الانكماش.

   ⚠️ التغيير فوري ولا يحتاج إعادة تشغيل الخدمة.
   ===================================================================== */

USE master;
GO

/* (1) القيم قبل التغيير */
SELECT name, value_in_use AS CurrentMB
FROM   sys.configurations
WHERE  name IN ('min server memory (MB)', 'max server memory (MB)');
GO

/* (2) التطبيق */
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;

EXEC sp_configure 'min server memory (MB)', 1024;
EXEC sp_configure 'max server memory (MB)', 1536;
RECONFIGURE;

EXEC sp_configure 'show advanced options', 0;
RECONFIGURE;
GO

/* (3) تحقق — المتوقّع 1024 و 1536 */
SELECT name, value_in_use AS NewMB
FROM   sys.configurations
WHERE  name IN ('min server memory (MB)', 'max server memory (MB)');
GO

/* (4) صفّر العدّادات بمراقبتها بعد يوم: لو timeout_error_count ثابت
       عند رقمه ولم يزد، فالمشكلة انتهت. */
SELECT  target_memory_kb/1024     AS TargetMB,
        available_memory_kb/1024  AS AvailableMB,
        waiter_count, timeout_error_count, forced_grant_count
FROM    sys.dm_exec_query_resource_semaphores
WHERE   resource_semaphore_id = 0;
GO

/* ---------------------------------------------------------------------
   للتراجع:
       EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
       EXEC sp_configure 'min server memory (MB)', 16;
       EXEC sp_configure 'max server memory (MB)', 2147483647;
       RECONFIGURE;
   --------------------------------------------------------------------- */
