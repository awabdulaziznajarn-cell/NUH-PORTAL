/* ==================================================================
   NUH-PORTAL - سعة الغرفة وحدّها الأقصى + أسلوب ترقيم المبنى
   ------------------------------------------------------------------
   شغّلها مرة واحدة على NUH_DB *قبل* نشر البناء الجديد.
   السكربت آمن للتكرار: كل خطوة بتتأكد الأول إن الحاجة مش موجودة.

   ⚠️ القيم دي من إدارة الإسكان مباشرة (اتصال ٣ سبتمبر ٢٠٢٦):
        الطلاب  : ٦٦ و٦٨ و٦٩ و٧٠ → ٣ في الغرفة
                  ٦٥ و٦٧          → ٢ في الغرفة، والمشرف يقدر يحطّ تالت
        الطالبات: كل المباني        → ٢ في الغرفة، والمشرفة تقدر تزوّد تالتة
   ================================================================== */
USE NUH_DB;
GO

/* ---------- 1) الحدّ الأقصى للغرفة ----------
   السعة (RoomCapacity) هي العدد المعتمد، ودي هي الحدّ اللي المشرف يقدر
   يوصّل له بالاستثناء. الفرق بينهم هو الفرق بين «المخطَّط» و«المسموح».

   ⚠️ من غير العمود ده كنّا هنبقى قدام اختيارين وحشين: نمنع إجراء الإدارة
      بتعمله فعلًا، أو نفتح الغرفة بلا سقف فمحدش يعرف إمتى تبقى فيها مشكلة.
      الغرفة بين الاتنين بتبان **مميَّزة بلون خاص** على الخريطة لا مخفية. */
IF COL_LENGTH('dbo.Buildings', 'RoomCapacityMax') IS NULL
BEGIN
    ALTER TABLE dbo.Buildings ADD RoomCapacityMax INT NOT NULL CONSTRAINT DF_Buildings_RoomCapacityMax DEFAULT (3);
    PRINT 'Buildings: added RoomCapacityMax (default 3)';
END
ELSE
    PRINT 'Buildings: RoomCapacityMax already exists - skipped';
GO

/* ---------- 2) أسلوب الترقيم ----------
   Continuous (سكن الطلاب) : الترقيم متّصل عبر المبنى.
       الأرضي شقق ١-٤ وغرفها ١-١٦، الدور ١ شقق ٥-٨ وغرفها ١٧-٣٢ ...
       يعني رقم الغرفة وحده بيحدّد شقتها ودورها.
   PerFloor (سكن الطالبات) : الترقيم بيبدأ من أول في كل دور.
       كل دور شقق ١-٤، وكل شقة غرفها ١-٤.
       يعني «شقة ٢ غرفة ٣» موجودة خمس مرات في المبنى - واحدة في كل دور.

   ⚠️ عمود لا اشتقاق من الجنس: مبنى جديد بأسلوب مختلف بيتظبط بصفّه، من غير
      نشر ولا كود. ونصّ لا رقم عشان صفّ المبنى يفضل مقروء من SSMS. */
IF COL_LENGTH('dbo.Buildings', 'Numbering') IS NULL
BEGIN
    ALTER TABLE dbo.Buildings ADD Numbering NVARCHAR(20) NOT NULL CONSTRAINT DF_Buildings_Numbering DEFAULT ('Continuous');
    PRINT 'Buildings: added Numbering (default Continuous)';
END
ELSE
    PRINT 'Buildings: Numbering already exists - skipped';
GO

/* ---------- 3) القيم المؤكَّدة ----------
   ⚠️ الترقيم بيتحدّد بجنس المبنى (القاعدة واحدة في كل مباني كل نوع)،
      أما السعة فبرقم المبنى - لأنها بتختلف جوّه سكن الطلاب نفسه. */

-- أسلوب الترقيم
UPDATE dbo.Buildings SET Numbering = 'Continuous' WHERE Gender = 'Male';
PRINT CONCAT('Buildings(Male)  : Numbering = Continuous -> rows: ', @@ROWCOUNT);

UPDATE dbo.Buildings SET Numbering = 'PerFloor'   WHERE Gender = 'Female';
PRINT CONCAT('Buildings(Female): Numbering = PerFloor   -> rows: ', @@ROWCOUNT);
GO

-- سعة الغرفة: الطلاب
UPDATE dbo.Buildings SET RoomCapacity = 3, RoomCapacityMax = 3
 WHERE Gender = 'Male' AND Code IN ('66', '68', '69', '70');
PRINT CONCAT('Buildings(66,68,69,70): capacity 3 / max 3 -> rows: ', @@ROWCOUNT);

UPDATE dbo.Buildings SET RoomCapacity = 2, RoomCapacityMax = 3
 WHERE Gender = 'Male' AND Code IN ('65', '67');
PRINT CONCAT('Buildings(65,67)      : capacity 2 / max 3 -> rows: ', @@ROWCOUNT);

-- سعة الغرفة: الطالبات
UPDATE dbo.Buildings SET RoomCapacity = 2, RoomCapacityMax = 3
 WHERE Gender = 'Female';
PRINT CONCAT('Buildings(Female)     : capacity 2 / max 3 -> rows: ', @@ROWCOUNT);
GO

/* ---------- 4) شوف النتيجة ---------- */
SELECT Code, Gender, Numbering, RoomCapacity, RoomCapacityMax, IsActive
FROM dbo.Buildings
ORDER BY Gender, Code;
GO
