-- ==================================================================
--  NUH-PORTAL - سكن اعضاء هيئة التدريس  [1/1] انشاء الجداول
-- ------------------------------------------------------------------
--  بينشئ جدولين:
--    FacultyUnits        الوحدات (ابراج + فلل) - الوحدة هي الثابت
--    FacultyOccupancies  سجل الاشغال - صف لكل ساكن لكل فترة
--
--  الملف امن للتشغيل اكتر من مرة: كل حاجة جوه IF NOT EXISTS.
--  مافيش اي مسح ولا تعديل على جداول موجودة.
--
--  ملاحظة: كل التعليقات بشرطتين عن قصد. SQL Server بيسمح بتعليقات
--  البلوك المتداخلة، فأي علامة فتح او قفل جوه تعليق بلوك بتقفله بدري
--  وتكسر الملف كله بعدها.
-- ==================================================================
USE NUH_DB;
GO

SET NOCOUNT ON;
GO

-- ------------------------------------------------------------------
-- 1) الوحدات
-- ------------------------------------------------------------------
IF OBJECT_ID('dbo.FacultyUnits', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FacultyUnits
    (
        Id                    INT IDENTITY(1,1) NOT NULL,

        -- 'tower' او 'villa' - نص مش رقم عشان الاستعلام المباشر يبقى مقروء
        UnitType              NVARCHAR(20)  NOT NULL,

        -- للابراج: البرج والشقة. للفلل: رقم الفيلا. الباقي NULL.
        -- بيفضلوا NULL لو اسم الحساب مخالف ومااتقراش - الوحدة بتفضل شغّالة
        TowerNo               INT           NULL,
        ApartmentNo           INT           NULL,
        VillaNo               INT           NULL,

        -- اسم الحساب في الدومين زي ما هو بالظبط - مفتاح الربط
        AdAccount             NVARCHAR(64)  NOT NULL,
        AdDistinguishedName   NVARCHAR(512) NULL,

        -- 'active' / 'out_of_service' / 'not_exists'
        Status                NVARCHAR(20)  NOT NULL CONSTRAINT DF_FacultyUnits_Status DEFAULT ('active'),

        -- اسم الحساب مطابق للمعيار؟ 0 = محتاج مراجعة (زي villa019 و ba8ap08)
        NameMatchesStandard   BIT           NOT NULL CONSTRAINT DF_FacultyUnits_NameStd DEFAULT (1),
        AdAccountEnabled      BIT           NOT NULL CONSTRAINT DF_FacultyUnits_AdEnabled DEFAULT (1),

        -- 'synced' / 'pending' / 'failed'
        SyncState             NVARCHAR(20)  NOT NULL CONSTRAINT DF_FacultyUnits_Sync DEFAULT ('synced'),
        LastSyncedAt          DATETIME2     NULL,
        LastSyncError         NVARCHAR(1000) NULL,

        Notes                 NVARCHAR(1000) NULL,

        CreatedAt             DATETIME2     NOT NULL CONSTRAINT DF_FacultyUnits_Created DEFAULT (SYSUTCDATETIME()),
        CreatedBy             INT           NULL,
        UpdatedAt             DATETIME2     NULL,
        UpdatedBy             INT           NULL,

        CONSTRAINT PK_FacultyUnits PRIMARY KEY CLUSTERED (Id)
    );

    PRINT 'FacultyUnits created.';
END
ELSE
    PRINT 'FacultyUnits already exists - skipped.';
GO

-- اسم الحساب لازم يكون فريد: لو اتكرر بقى عندنا وحدتين بيكتبوا فوق بعض
-- في نفس حساب الدومين، وآخر واحد بيحفظ بيمسح اللي قبله من غير ما حد يعرف
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FacultyUnits_ad_account' AND object_id = OBJECT_ID('dbo.FacultyUnits'))
    CREATE UNIQUE INDEX UX_FacultyUnits_ad_account ON dbo.FacultyUnits (AdAccount);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacultyUnits_tower' AND object_id = OBJECT_ID('dbo.FacultyUnits'))
    CREATE INDEX IX_FacultyUnits_tower ON dbo.FacultyUnits (UnitType, TowerNo, ApartmentNo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacultyUnits_villa' AND object_id = OBJECT_ID('dbo.FacultyUnits'))
    CREATE INDEX IX_FacultyUnits_villa ON dbo.FacultyUnits (UnitType, VillaNo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacultyUnits_status' AND object_id = OBJECT_ID('dbo.FacultyUnits'))
    CREATE INDEX IX_FacultyUnits_status ON dbo.FacultyUnits (Status);
GO

-- ------------------------------------------------------------------
-- 2) سجل الاشغال
-- ------------------------------------------------------------------
IF OBJECT_ID('dbo.FacultyOccupancies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FacultyOccupancies
    (
        Id                INT IDENTITY(1,1) NOT NULL,
        UnitId            INT            NOT NULL,

        -- الاسم العربي بيتكتب في description بتاع حساب الوحدة
        FullNameAr        NVARCHAR(250)  NOT NULL,
        -- 'male' / 'female' - جنس الساكن. مش على الوحدة لان الوحدات مختلطة:
        -- نفس الشقة ممكن تكون فيها دكتورة النهاردة ودكتور بعد سنة
        Gender            NVARCHAR(20)   NULL,
        -- الهوية بتتكتب في employeeID والجوال في mobile
        NationalId        NVARCHAR(20)   NULL,
        Mobile            NVARCHAR(20)   NULL,
        -- الكلية والقسم بيتكتبوا في company و department
        College           NVARCHAR(200)  NULL,
        Department        NVARCHAR(200)  NULL,

        StartDate         DATETIME2      NOT NULL,
        -- NULL = الساكن الحالي. اي قيمة هنا معناها الصف اتقفل ومابيتعدلش تاني
        EndDate           DATETIME2      NULL,
        -- 'contract_ended' / 'transferred' / 'left_permanently' / 'other'
        EndReason         NVARCHAR(30)   NULL,
        EndReasonNote     NVARCHAR(500)  NULL,

        -- رقم تذكرة انجاز - الرابط الوحيد بين الطلب هناك والتنفيذ هنا
        TicketNo          NVARCHAR(50)   NULL,
        -- بوليسي الجامعة: اي حساب جديد لازم يعدي على الامن السيبراني الاول
        CyberApprovedAt   DATETIME2      NULL,

        AttachmentPath    NVARCHAR(500)  NULL,
        OriginalFileName  NVARCHAR(260)  NULL,

        -- الصف ده اتعمل من الاستيراد الاولي؟ لو 1 فتاريخ البداية تقديري
        -- مش حقيقي - الدومين مابيعرفش الساكن دخل امتى
        ImportedFromAd    BIT            NOT NULL CONSTRAINT DF_FacultyOcc_Imported DEFAULT (0),

        -- اخر تأكيد ان الساكن لسه موجود
        ConfirmedAt       DATETIME2      NULL,
        ConfirmedBy       INT            NULL,

        CreatedBy         INT            NULL,
        CreatedAt         DATETIME2      NOT NULL CONSTRAINT DF_FacultyOcc_Created DEFAULT (SYSUTCDATETIME()),
        ClosedBy          INT            NULL,

        CONSTRAINT PK_FacultyOccupancies PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_FacultyOccupancies_Unit FOREIGN KEY (UnitId)
            REFERENCES dbo.FacultyUnits (Id) ON DELETE CASCADE
    );

    PRINT 'FacultyOccupancies created.';
END
ELSE
    PRINT 'FacultyOccupancies already exists - skipped.';
GO

-- ------------------------------------------------------------------
-- 3) القاعدة الاهم: ساكن واحد مفتوح لكل وحدة
-- ------------------------------------------------------------------
-- ⚠️ القاعدة دي على مستوى قاعدة البيانات مش على مستوى الكود. الفحص في
--    الكود بيتنسى في اي مسار جديد (استيراد، تصحيح، شاشة تانية) وبيتكسر
--    مع طلبين في نفس اللحظة. الفهرس ده مايتكسرش لا بده ولا بده.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FacultyOccupancies_open' AND object_id = OBJECT_ID('dbo.FacultyOccupancies'))
    CREATE UNIQUE INDEX UX_FacultyOccupancies_open
        ON dbo.FacultyOccupancies (UnitId)
        WHERE EndDate IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacultyOccupancies_unit' AND object_id = OBJECT_ID('dbo.FacultyOccupancies'))
    CREATE INDEX IX_FacultyOccupancies_unit ON dbo.FacultyOccupancies (UnitId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacultyOccupancies_national_id' AND object_id = OBJECT_ID('dbo.FacultyOccupancies'))
    CREATE INDEX IX_FacultyOccupancies_national_id ON dbo.FacultyOccupancies (NationalId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacultyOccupancies_ticket' AND object_id = OBJECT_ID('dbo.FacultyOccupancies'))
    CREATE INDEX IX_FacultyOccupancies_ticket ON dbo.FacultyOccupancies (TicketNo);
GO

-- ------------------------------------------------------------------
-- 4) الروابط بجدول المستخدمين
-- ------------------------------------------------------------------
-- ⚠️ NO ACTION مش CASCADE: مسح موظف من النظام مايمسحش تاريخ الوحدات اللي
--    هو نفّذ عليها اجراءات. الاسم بيفضل منسوب لاجراءاته زي سجل العمليات.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FacultyUnits_CreatedBy')
    ALTER TABLE dbo.FacultyUnits WITH NOCHECK
        ADD CONSTRAINT FK_FacultyUnits_CreatedBy FOREIGN KEY (CreatedBy)
            REFERENCES dbo.Users (Id) ON DELETE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FacultyUnits_UpdatedBy')
    ALTER TABLE dbo.FacultyUnits WITH NOCHECK
        ADD CONSTRAINT FK_FacultyUnits_UpdatedBy FOREIGN KEY (UpdatedBy)
            REFERENCES dbo.Users (Id) ON DELETE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FacultyOcc_CreatedBy')
    ALTER TABLE dbo.FacultyOccupancies WITH NOCHECK
        ADD CONSTRAINT FK_FacultyOcc_CreatedBy FOREIGN KEY (CreatedBy)
            REFERENCES dbo.Users (Id) ON DELETE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FacultyOcc_ClosedBy')
    ALTER TABLE dbo.FacultyOccupancies WITH NOCHECK
        ADD CONSTRAINT FK_FacultyOcc_ClosedBy FOREIGN KEY (ClosedBy)
            REFERENCES dbo.Users (Id) ON DELETE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FacultyOcc_ConfirmedBy')
    ALTER TABLE dbo.FacultyOccupancies WITH NOCHECK
        ADD CONSTRAINT FK_FacultyOcc_ConfirmedBy FOREIGN KEY (ConfirmedBy)
            REFERENCES dbo.Users (Id) ON DELETE NO ACTION;
GO

-- ------------------------------------------------------------------
-- 5) تأكيد
-- ------------------------------------------------------------------
SELECT
    (SELECT COUNT(*) FROM sys.tables  WHERE name IN ('FacultyUnits','FacultyOccupancies'))                       AS [الجداول],
    (SELECT COUNT(*) FROM sys.indexes WHERE name LIKE 'UX_Faculty%' OR name LIKE 'IX_Faculty%')                   AS [الفهارس],
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE name LIKE 'FK_Faculty%')                                        AS [الروابط];

SELECT COUNT(*) AS [عدد الوحدات المسجّلة] FROM dbo.FacultyUnits;
GO

PRINT 'Done. Faculty housing schema is ready.';
GO
