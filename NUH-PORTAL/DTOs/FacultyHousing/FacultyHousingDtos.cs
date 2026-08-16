using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.FacultyHousing
{
    // ---------- فحص الصلاحية ----------
    public class OuAccessDto
    {
        public string OrganizationalUnit { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;   // «أبراج — بنين» ...
        public bool OuExists { get; set; }
        public bool CanRead { get; set; }
        public bool AllWritable { get; set; }
        public string? ProbedAccount { get; set; }
        public Dictionary<string, bool> AttributeWritable { get; set; } = new();
        public string? Error { get; set; }
    }

    public class AccessCheckDto
    {
        public bool Configured { get; set; }
        public bool AllOk { get; set; }
        public List<OuAccessDto> Ous { get; set; } = new();
    }

    // ---------- الاستيراد ----------
    public enum ImportRowAction { New = 1, Unchanged = 2, Changed = 3, Ignored = 4 }

    public class ImportRowDto
    {
        public string AdAccount { get; set; } = string.Empty;
        public string? DistinguishedName { get; set; }
        public string OrganizationalUnit { get; set; } = string.Empty;
        public ImportRowAction Action { get; set; }
        public string ActionLabel { get; set; } = string.Empty;

        public FacultyUnitType? UnitType { get; set; }
        public int? TowerNo { get; set; }
        public int? ApartmentNo { get; set; }
        public int? VillaNo { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public bool NameMatchesStandard { get; set; }
        public string? Deviation { get; set; }

        // اللي مكتوب في الدومين دلوقتي
        public string? Description { get; set; }
        public string? EmployeeId { get; set; }
        public string? Mobile { get; set; }
        public string? Company { get; set; }
        public string? Department { get; set; }
        public bool AccountEnabled { get; set; }

        // سبب التجاهل لو Action = Ignored
        public string? IgnoreReason { get; set; }
    }

    public class ImportPreviewDto
    {
        public bool Success { get; set; }
        public List<ImportRowDto> Rows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        // وصلنا لسقف القراءة وفي كمان — تحذير صريح مش صمت
        public bool Truncated { get; set; }

        public int NewCount { get; set; }
        public int UnchangedCount { get; set; }
        public int ChangedCount { get; set; }
        public int IgnoredCount { get; set; }
        public int DeviationCount { get; set; }
        public int WithOccupantCount { get; set; }
    }

    public class ImportApplyResultDto
    {
        public int UnitsCreated { get; set; }
        public int UnitsUpdated { get; set; }
        public int OccupanciesOpened { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // ---------- شاشة الوحدات ----------
    public class FacultyUnitListItemDto
    {
        public int Id { get; set; }
        public string AdAccount { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public FacultyUnitType UnitType { get; set; }
        public int? TowerNo { get; set; }
        public int? ApartmentNo { get; set; }
        public int? VillaNo { get; set; }
        public FacultyUnitStatus Status { get; set; }
        public FacultyUnitSyncState SyncState { get; set; }
        public bool NameMatchesStandard { get; set; }
        public bool AdAccountEnabled { get; set; }
        public string? LastSyncError { get; set; }

        // الساكن الحالي — null يعني الوحدة شاغرة
        public int? OccupancyId { get; set; }
        public string? OccupantName { get; set; }
        public string? OccupantNationalId { get; set; }
        public string? OccupantMobile { get; set; }
        public Gender? OccupantGender { get; set; }
        public DateTime? OccupantSince { get; set; }
        public bool OccupantImported { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        // عدد الشهور من آخر تأكيد أو من بداية السكن — بيغذّي شاشة تأكيد الإشغال
        public int? MonthsSinceConfirmed { get; set; }

        // ⚠️ الحساب قاعد في OU بنين والساكن دكتورة أو العكس.
        //    قرار مقصود: النظام مابينقلش الحساب بين الـ OUs — النقل محتاج صلاحية
        //    حذف وإنشاء كائنات، وهي أخطر بكتير من كتابة خمس خصائص. بدل ما
        //    التقسيم يغلط في صمت لسنين، بنعدّ المخالفات ونعرضها، وبعد كام شهر
        //    الرقم نفسه هو اللي يقرّر: نسيب التقسيم ولا ندمج.
        public bool OuGenderMismatch { get; set; }
        public string? OuGenderMismatchNote { get; set; }
    }

    public class FacultyUnitsPageDto
    {
        public List<FacultyUnitListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int TotalUnits { get; set; }
        public int Occupied { get; set; }
        public int Vacant { get; set; }
        public int OutOfService { get; set; }
        public int NotExists { get; set; }
        public int NeedsConfirm { get; set; }
        public int PendingSync { get; set; }
        public int NameDeviations { get; set; }
        public int OuMismatches { get; set; }
        // أرقام الأبراج الموجودة فعلًا — لملء قائمة الفلتر
        public List<int> Towers { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    // ---------- سجل الوحدة ----------
    public class OccupancyHistoryItemDto
    {
        public int Id { get; set; }
        public string FullNameAr { get; set; } = string.Empty;
        public Gender? Gender { get; set; }
        public string? NationalId { get; set; }
        public string? Mobile { get; set; }
        public string? College { get; set; }
        public string? Department { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public OccupancyEndReason? EndReason { get; set; }
        public string? EndReasonNote { get; set; }
        public string? TicketNo { get; set; }
        public DateTime? CyberApprovedAt { get; set; }
        public string? OriginalFileName { get; set; }
        public bool ImportedFromAd { get; set; }
        public bool IsCurrent { get; set; }
        public string? CreatedByName { get; set; }
        public string? ClosedByName { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }

    public class FacultyUnitDetailDto
    {
        public FacultyUnitListItemDto Unit { get; set; } = new();
        public string? DistinguishedName { get; set; }
        public string? Notes { get; set; }
        public DateTime? LastSyncedAt { get; set; }
        public List<OccupancyHistoryItemDto> History { get; set; } = new();

        // ⚠️ سجل الإشغال يحفظ *النتيجة*: من يسكن الوحدة الآن ومن سكنها قبله.
        //    وهذا يصف الحاضر لا الإجراء: لا يقول من عدّل رقم الهوية ولا متى ولا
        //    ما كانت قيمته قبل التعديل. سجل العمليات أدناه يحفظ الإجراء نفسه،
        //    وهو المطلوب في أي تقرير مساءلة.
        public List<FacultyAuditItemDto> Changes { get; set; } = new();
    }

    // بند واحد في سجل عمليات الوحدة - إجراء بمنفّذه ووقته وحقوله المتغيّرة.
    public class FacultyAuditItemDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = "";
        public DateTime ActionAt { get; set; }
        public string? ActorName { get; set; }
        public string? IpAddress { get; set; }
        public List<FacultyAuditFieldDto> Fields { get; set; } = new();
    }

    public class FacultyAuditFieldDto
    {
        public string FieldName { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    // ---------- التسليم ----------
    public class HandoverRequestDto
    {
        public int UnitId { get; set; }
        public FacultyRequestType RequestType { get; set; }

        // ⚠️ اختياري هنا وإجباري في الخدمة عند التسليم وحده. تعريفه إجباريًا في
        //    الـ DTO كان يجعل ASP.NET يرفض الطلب قبل وصوله للخدمة، فتفشل شاشة
        //    «تعديل البيانات» برسالة عامة لا تدل على شيء - وهي شاشة تصحيح لا
        //    طلب في إنجاز، فلا رقم معاملة لها. القاعدة محلّها الخدمة حيث يُعرف
        //    السياق: تسليم أم تصحيح.
        public string? TicketNo { get; set; }
        public DateTime? CyberApprovedAt { get; set; }

        // الساكن الجديد — مطلوب في NewService و ChangeOccupant
        public string? FullNameAr { get; set; }
        public Gender? Gender { get; set; }
        public string? NationalId { get; set; }
        public string? Mobile { get; set; }
        public string? College { get; set; }
        public string? Department { get; set; }
        public DateTime? StartDate { get; set; }

        // قفل الساكن الحالي
        public DateTime? EndDate { get; set; }
        public OccupancyEndReason? EndReason { get; set; }
        public string? EndReasonNote { get; set; }

        // يكتب في الدومين على طول ولا يستنى زرار «تطبيق»؟
        public bool PushToAd { get; set; }
    }

    // ---------- لوحة الفرق قبل الكتابة في الدومين ----------
    public class AdDiffLineDto
    {
        public string Attribute { get; set; } = string.Empty;
        public string? CurrentValue { get; set; }
        public string? NewValue { get; set; }
        public bool WillChange { get; set; }
        public string? Note { get; set; }
    }

    public class AdDiffDto
    {
        public int UnitId { get; set; }
        public string AdAccount { get; set; } = string.Empty;
        public string? DistinguishedName { get; set; }
        public bool AccountFound { get; set; }
        public List<AdDiffLineDto> Lines { get; set; } = new();
        public int ChangeCount { get; set; }
        public string? Error { get; set; }
    }

    public class AdPushResultDto
    {
        public bool Success { get; set; }
        public int AttributesWritten { get; set; }
        public string? Error { get; set; }
        public FacultyUnitSyncState SyncState { get; set; }
    }
}
