/* ============================================================================
   سجل حركة التسكين - إنشاء الجدول وتعبئته من الموجود
   قاعدة البيانات: NUH_DB

   يُشغَّل مرّة واحدة. الخطوات ١ و٢ آمنتان تمامًا (إنشاء جدول جديد لا يمسّ
   جدولًا قائمًا). الخطوة ٣ هي التعبئة الأوّلية، وهي داخل معاملة والـCOMMIT
   معلَّق في آخرها - راجع نتيجة المعاينة أوّلًا ثم نفّذ COMMIT بنفسك.

   ⚠️ شغّل الخطوة ١ و٢ قبل نشر الكود الجديد: التطبيق بعد النشر يكتب في هذا
      الجدول مع كل تسكين، وغيابه يجعل كل عملية تسكين تفشل.
   ============================================================================ */

USE NUH_DB;
GO

/* ---------------------------------------------------------------------------
   ١ - الجدول
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.HousingHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HousingHistory
    (
        Id              INT IDENTITY(1,1) NOT NULL,
        student_id      INT           NOT NULL,
        student_number  NVARCHAR(50)  NULL,
        action          NVARCHAR(20)  NOT NULL,
        source          NVARCHAR(24)  NULL,

        from_building   NVARCHAR(50)  NULL,
        from_floor      NVARCHAR(10)  NULL,
        from_apartment  NVARCHAR(10)  NULL,
        from_room       NVARCHAR(10)  NULL,

        to_building     NVARCHAR(50)  NULL,
        to_floor        NVARCHAR(10)  NULL,
        to_apartment    NVARCHAR(10)  NULL,
        to_room         NVARCHAR(10)  NULL,

        reason          NVARCHAR(400) NULL,
        transfer_id     INT           NULL,
        created_by      INT           NOT NULL,
        created_at      DATETIME2     NOT NULL,

        CONSTRAINT PK_HousingHistory PRIMARY KEY CLUSTERED (Id),

        /* ⚠️ NO ACTION على الطالب لا CASCADE: حذف صفّ الطالب يجب ألّا يمحو
           تاريخ سكنه - ولهذا كذلك الرقم الجامعي منسوخ نصًّا في الصفّ. */
        CONSTRAINT FK_HousingHistory_Student
            FOREIGN KEY (student_id)  REFERENCES dbo.Students(Id),
        CONSTRAINT FK_HousingHistory_User
            FOREIGN KEY (created_by)  REFERENCES dbo.Users(Id),
        CONSTRAINT FK_HousingHistory_Transfer
            FOREIGN KEY (transfer_id) REFERENCES dbo.HousingTransfers(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_HousingHistory_Student
        ON dbo.HousingHistory (student_id, created_at);

    /* فهرس تاريخ الغرفة - يُستعلَم مع كل فتح غرفة من الخريطة */
    CREATE INDEX IX_HousingHistory_Room
        ON dbo.HousingHistory (to_building, to_floor, to_apartment, to_room);

    PRINT 'HousingHistory: تم إنشاء الجدول والفهرسين';
END
ELSE
    PRINT 'HousingHistory: الجدول موجود - لم يُنشأ من جديد';
GO

/* ---------------------------------------------------------------------------
   ٢ - معاينة ما ستكتبه التعبئة الأوّلية (قراءة فقط)

   ⚠️ التعبئة ضرورية لا تحسينية: بغيرها يفتح المشرف السجل أوّل يوم فيجده
      فارغًا لطالب يعرف أنه انتقل مرّتين، فيفقد الثقة فيه ولا يعود يفتحه.
   --------------------------------------------------------------------------- */
SELECT 'صفوف نقل قائمة ستُنقل إلى السجل' AS البيان, COUNT(*) AS العدد
FROM dbo.HousingTransfers
UNION ALL
SELECT 'طلاب مسكَّنون الآن سيُكتب لهم صفّ تسكين', COUNT(*)
FROM dbo.Students
WHERE IsDeleted = 0 AND room_number IS NOT NULL AND LTRIM(RTRIM(room_number)) <> ''
UNION ALL
SELECT 'صفوف موجودة في السجل الآن', COUNT(*) FROM dbo.HousingHistory;
GO

/* ---------------------------------------------------------------------------
   ٣ - التعبئة الأوّلية
   --------------------------------------------------------------------------- */
BEGIN TRANSACTION;

/* أ - صفوف النقل القائمة: كل صفّ في HousingTransfers يصير صفّ حركة يشير إليه.
      وصفوف «المغادرة» التي كُتبت هناك كحيلة قبل وجود هذا الجدول (موضعها
      الجديد فارغ) تُقرأ إخلاءً لا نقلًا - وهو ما هي عليه فعلًا. */
INSERT INTO dbo.HousingHistory
    (student_id, student_number, action, source,
     from_building, from_floor, from_apartment, from_room,
     to_building, to_floor, to_apartment, to_room,
     reason, transfer_id, created_by, created_at)
SELECT
    t.student_id,
    t.student_number,
    CASE WHEN ISNULL(NULLIF(LTRIM(RTRIM(t.new_building)), ''), '') = ''
              AND ISNULL(NULLIF(LTRIM(RTRIM(t.new_room)), ''), '') = ''
         THEN 'cleared' ELSE 'transferred' END,
    CASE WHEN t.reason LIKE 'departure[_]%' THEN 'status' ELSE 'transfer' END,
    NULLIF(LTRIM(RTRIM(t.old_building)), ''),
    NULLIF(LTRIM(RTRIM(t.OldFloor)),     ''),
    NULLIF(LTRIM(RTRIM(t.old_apartment)),''),
    NULLIF(LTRIM(RTRIM(t.old_room)),     ''),
    NULLIF(LTRIM(RTRIM(t.new_building)), ''),
    NULLIF(LTRIM(RTRIM(t.NewFloor)),     ''),
    NULLIF(LTRIM(RTRIM(t.new_apartment)),''),
    NULLIF(LTRIM(RTRIM(t.new_room)),     ''),
    ISNULL(t.custom_reason, t.reason),
    t.Id,
    t.created_by,
    t.created_at
FROM dbo.HousingTransfers t
WHERE NOT EXISTS (SELECT 1 FROM dbo.HousingHistory h WHERE h.transfer_id = t.Id);

/* ب - الطلاب المسكَّنون الآن: صفّ تسكين واحد لكل طالب لا يوجد له في السجل
      صفّ يصل إلى موضعه الحالي.

   ⚠️ التاريخ المكتوب هو تاريخ إنشاء صفّ الطالب لا وقت تشغيل السكربت: تسكين
      وقع قبل شهر لا يصحّ أن يظهر في السجل بتاريخ اليوم. وإن لم يكن للصفّ
      تاريخ إنشاء يُكتب وقت التشغيل - وهو أقرب ما يمكن معرفته.

   ⚠️ وcreated_by = 0 غير مقبول (مفتاح أجنبي على Users). يُستخدم أدنى رقم
      مستخدم في الجدول ليمثّل «تعبئة أوّلية»، والمصدر backfill يميّزها. */
DECLARE @SystemUserId INT = (SELECT MIN(Id) FROM dbo.Users);

INSERT INTO dbo.HousingHistory
    (student_id, student_number, action, source,
     to_building, to_floor, to_apartment, to_room,
     reason, created_by, created_at)
SELECT
    s.Id,
    s.student_id,
    'assigned',
    'backfill',
    NULLIF(LTRIM(RTRIM(s.housing_building)), ''),
    NULLIF(LTRIM(RTRIM(s.floor_number)),     ''),
    NULLIF(LTRIM(RTRIM(s.apartment_number)), ''),
    NULLIF(LTRIM(RTRIM(s.room_number)),      ''),
    N'تعبئة أوّلية عند إنشاء سجل حركة التسكين - التسكين وقع قبل تشغيل السجل',
    @SystemUserId,
    ISNULL(s.created_at, SYSUTCDATETIME())
FROM dbo.Students s
WHERE s.IsDeleted = 0
  AND ISNULL(LTRIM(RTRIM(s.room_number)), '') <> ''
  AND NOT EXISTS (
        SELECT 1 FROM dbo.HousingHistory h
        WHERE h.student_id = s.Id
          AND ISNULL(h.to_room, '') = ISNULL(LTRIM(RTRIM(s.room_number)), '')
          AND ISNULL(h.to_building, '') = ISNULL(LTRIM(RTRIM(s.housing_building)), '')
  );

/* ---------------------------------------------------------------------------
   ٤ - مراجعة النتيجة قبل الاعتماد
   --------------------------------------------------------------------------- */
SELECT action AS الإجراء, source AS المصدر, COUNT(*) AS العدد
FROM dbo.HousingHistory
GROUP BY action, source
ORDER BY العدد DESC;

SELECT TOP 20
    student_number AS الرقم_الجامعي, action AS الإجراء, source AS المصدر,
    from_building AS من_مبنى, from_floor AS من_دور, from_apartment AS من_شقة, from_room AS من_غرفة,
    to_building   AS إلى_مبنى, to_floor  AS إلى_دور, to_apartment  AS إلى_شقة, to_room  AS إلى_غرفة,
    created_at AS التاريخ
FROM dbo.HousingHistory
ORDER BY Id DESC;

/* راجع الجدولين أعلاه. إن كانت الأرقام كما تتوقّع، أزل التعليق ونفّذ: */
-- COMMIT TRANSACTION;

/* وإن كان فيها خلل: */
-- ROLLBACK TRANSACTION;

/* ⚠️ لا تترك النافذة مفتوحة على معاملة غير منتهية - المعاملة المفتوحة تحجز
      الجدول وتُعلِّق كل من يكتب فيه. للتأكّد: SELECT @@TRANCOUNT; يجب أن يكون صفرًا. */
GO
