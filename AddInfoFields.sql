/* ==================================================================
   NUH-PORTAL — الخانات المطلوب من الطالب تصحيحها
   ------------------------------------------------------------------
   شغّلها مرة واحدة على NUH_DB *قبل* نشر البناء الجديد.
   السكربت آمن للتكرار.
   ================================================================== */
USE NUH_DB;
GO

/* مفاتيح الخانات مفصولة بفاصلة (مثال: housing_building,room_number).
   فاضي = كل الخانات مفتوحة للطالب — وده حال أي طلب اترجّع قبل الميزة دي،
   فمفيش طلب قديم هيتقفل فجأة على صاحبه.                              */
IF COL_LENGTH('dbo.Requests', 'info_fields') IS NULL
BEGIN
    ALTER TABLE dbo.Requests ADD info_fields NVARCHAR(500) NULL;
    PRINT 'Requests: added info_fields';
END
ELSE
    PRINT 'Requests: info_fields already exists - skipped';
GO

/* تأكيد */
SELECT COUNT(*) AS [طلبات مرجّعة للطالب حاليًا]
FROM   dbo.Requests WHERE status = 'need_more_info';
GO
