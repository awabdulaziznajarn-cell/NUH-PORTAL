/* ==================================================================
   NUH-PORTAL - سعة الغرفة على جدول المباني + تنظيف السكن المعلَّق
   ------------------------------------------------------------------
   شغّلها مرة واحدة على NUH_DB *قبل* نشر البناء الجديد.
   السكربت آمن للتكرار: كل خطوة بتتأكد الأول إن الحاجة مش موجودة.

   ⚠️ الخطوة (3) بتعدّل بيانات - اقراها قبل ما تشغّلها.
   ================================================================== */
USE NUH_DB;
GO

/* ---------- 1) عمود سعة الغرفة ----------
   عدد الساكنين المسموح بهم في الغرفة الواحدة داخل المبنى ده.

   ليه على المبنى لا على الغرفة: البنية واحدة في كل المباني (٢٠ شقة × ٤ غرف
   = ٨٠ غرفة)، واللي بيختلف هو السعة بين سكن الطالبات وسكن الطلاب. عمود واحد
   بدل جدول ٨٠٠ صف مالوش أي معلومة غير رقم متكرّر.

   الافتراضي ٢: لو مبنى جديد اتضاف ونُسي، الأقلّ أأمن من الأكتر - النظام
   هيقول «الغرفة مكتملة» بدري بدل ما يحطّ طالب في مكان مش موجود.          */
IF COL_LENGTH('dbo.Buildings', 'RoomCapacity') IS NULL
BEGIN
    ALTER TABLE dbo.Buildings ADD RoomCapacity INT NOT NULL CONSTRAINT DF_Buildings_RoomCapacity DEFAULT (2);
    PRINT 'Buildings: added RoomCapacity (default 2)';
END
ELSE
    PRINT 'Buildings: RoomCapacity already exists - skipped';
GO

/* ---------- 2) القيم الحالية ----------
   سكن الطالبات: بنتان في الغرفة (مؤكَّد).
   سكن الطلاب : ثلاثة (مبدئيًّا - لحد ما إدارة الإسكان تأكّد، وساعتها
                التعديل سطر UPDATE واحد هنا، مافيش نشر ولا كود).

   ⚠️ بيتحدّد بجنس المبنى لا برقمه: لو اتضاف مبنى جديد، بياخد سعته من نوعه
      تلقائيًّا بدل ما حد يفتكر يحدّث قائمة أرقام.                        */
UPDATE dbo.Buildings SET RoomCapacity = 2 WHERE Gender = 'Female';
PRINT CONCAT('Buildings(Female): set RoomCapacity = 2  -> rows: ', @@ROWCOUNT);

UPDATE dbo.Buildings SET RoomCapacity = 3 WHERE Gender = 'Male';
PRINT CONCAT('Buildings(Male)  : set RoomCapacity = 3  -> rows: ', @@ROWCOUNT);
GO

/* ---------- 3) تنظيف السكن المعلَّق على طلاب غادروا ----------
   الكود القديم كان بيفرّغ السكن لما الحالة «ترك الإسكان» وبس. الطلاب اللي
   اتخرّجوا أو اتفصلوا أو اتحوّلوا لسه غرفهم مسجّلة عليهم - فخريطة الإشغال
   هتوريها مشغولة وهي فاضية.

   الكود اتصلّح (StudentStatusService)، والخطوة دي بتنضّف اللي اتسجّل قبل
   الإصلاح.

   ⚠️ «أخرى» مستثناة عن قصد: حالة غير محدَّدة، ومانعرفش لو الطالب ساب
      السكن ولا لأ - وتفريغ غرفة طالب لسه ساكن فيها أسوأ من بيانات قديمة.

   ⚠️ شوف الأول قبل ما تنفّذ: شغّل SELECT وبصّ على العدد.                  */

-- (أ) شوف الأول
SELECT student_status, COUNT(*) AS Students
FROM dbo.Students
WHERE IsDeleted = 0
  AND student_status IN ('graduated', 'dismissed', 'transferred')
  AND (housing_building IS NOT NULL OR BuildingId IS NOT NULL
       OR floor_number IS NOT NULL OR apartment_number IS NOT NULL OR room_number IS NOT NULL)
GROUP BY student_status;
GO

-- (ب) نفّذ
BEGIN TRANSACTION;

UPDATE dbo.Students
   SET housing_building = NULL,
       BuildingId       = NULL,
       floor_number     = NULL,
       apartment_number = NULL,
       room_number      = NULL
 WHERE IsDeleted = 0
   AND student_status IN ('graduated', 'dismissed', 'transferred');

PRINT CONCAT('Students: cleared stale housing -> rows: ', @@ROWCOUNT);

-- راجع العدد فوق، وبعدين شيل التعليق عن السطر اللي تحت:
-- COMMIT TRANSACTION;
-- ولو مش عاجبك:
-- ROLLBACK TRANSACTION;
GO
