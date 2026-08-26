using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.FacultyHousing
{
    // ---------- فحص الصلاحية ----------
    public class OuAccessDto
    {
        public string OrganizationalUnit { get; set; } = string.Empty;
        // ⚠️ مفتاح ترجمة لا نصّ. الترجمة في الواجهة عبر FH_T.
        public string LabelKey { get; set; } = string.Empty;
        // الاسم التقني للوحدة في الدليل (MALE / FEMALE / Villas) — مُشتقّ من المسار.
        public string OuName { get; set; } = string.Empty;
        public bool OuExists { get; set; }
        public bool CanRead { get; set; }
        public bool AllWritable { get; set; }
        // إنشاء حساب جوّه الوحدة — بيلزم لنقل الحسابات بين قسمَي الدليل.
        // null = الدومين ما رجّعش الخاصية المحسوبة، مش «مرفوض».
        public bool? CanCreateUser { get; set; }
        // ⚠️ هل هذه الوحدة طرفٌ في النقل أصلًا؟ «الفلل» مختلطة، فلا نقل
        //    منها ولا إليها، وعرض «مرفوض» في خانتها يصف نقصًا لا وجود له.
        public bool IsGendered { get; set; }
        // الكتابة على اسم الكائن (cn / name) — الشرط التالت للنقل
        public bool CanWriteRdn { get; set; }
        public Dictionary<string, bool> RdnAttributeWritable { get; set; } = new();
        public string? ProbedAccount { get; set; }
        public Dictionary<string, bool> AttributeWritable { get; set; } = new();
        public string? Error { get; set; }
    }

    public class AccessCheckDto
    {
        public bool Configured { get; set; }
        // القراءة والكتابة في الخصائص الخمس — ده اللي الاستيراد محتاجه
        public bool AllOk { get; set; }
        // ⚠️ صلاحية إنشاء حساب في كل الأقسام المقسّمة بالجنس - نص شرط نقل
        //    الحسابات بينها. النص التاني (الحذف من القسم المصدر) مالوش خاصية
        //    محسوبة نسأل عنها الدومين، فمابندّعيش إننا فحصناه.
        public bool MoveCreateOk { get; set; }
        // الكتابة على اسم الكائن في كل الأقسام المقسّمة بالجنس
        public bool MoveRdnOk { get; set; }
        // فيه أقسام مقسّمة بالجنس أصلًا؟ (الفلل ممكن تكون وحدة واحدة مختلطة)
        public bool HasGenderedOus { get; set; }
        public List<OuAccessDto> Ous { get; set; } = new();
    }

    // ---------- الاستيراد ----------
    public enum ImportRowAction { New = 1, Unchanged = 2, Changed = 3, Ignored = 4 }

    public class ImportRowDto
    {
        public string AdAccount { get; set; } = string.Empty;
        public string? DistinguishedName { get; set; }
        // ⚠️ مفتاح ترجمة لا نصّ — انظر OuAccessDto.LabelKey.
        public string OrganizationalUnitKey { get; set; } = string.Empty;
        public string OuName { get; set; } = string.Empty;
        public ImportRowAction Action { get; set; }

        public FacultyUnitType? UnitType { get; set; }
        public int? TowerNo { get; set; }
        public int? ApartmentNo { get; set; }
        public int? VillaNo { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public bool NameMatchesStandard { get; set; }
        public string? Deviation { get; set; }
        // ⚠️ «سيتم تحديثها» من غير ما تقول إيه هيتحدّث بتخلّي اللي بيراجع
        //    المعاينة يوافق على تغيير مش شايفه. السطر ده بيقول بالظبط أنهي
        //    حاجة اختلفت عن المسجَّل عندنا.
        // ⚠️ مفاتيح ترجمة لا نصوص — انظر OrganizationalUnitKey.
        public List<string> ChangeNoteKeys { get; set; } = new();

        // اللي مكتوب في الدومين دلوقتي
        public string? Description { get; set; }
        // ⚠️ ‏AdDisplayName لا DisplayName: الحقل اللي فوق اسم **الوحدة**
        //    المعروض («برج 1 - شقة 3»)، وده اسم **الساكن** بالإنجليزي كما هو
        //    في الدليل. الاسمان المتشابهان في نفس الكائن كانا هيتبدّلوا في أول
        //    تعديل، والقيمتان مختلفتان تمامًا في المعنى.
        public string? AdDisplayName { get; set; }
        public string? AdUserPrincipalName { get; set; }
        public string? EmployeeId { get; set; }
        public string? Mobile { get; set; }
        public string? Company { get; set; }
        public string? Department { get; set; }
        public bool AccountEnabled { get; set; }

        // سبب التجاهل لو Action = Ignored — مفتاح ترجمة لا نصّ.
        public string? IgnoreReasonKey { get; set; }
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
        // الاسم الإنجليزي - يُعرض سطرًا ثانيًا تحت العربي في الجدول
        public string? OccupantNameEn { get; set; }
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

        // اسم الدخول الكامل في الدليل، وهل مقدّمته تطابق اسم الحساب.
        // ⚠️ المطابقة true لمّا يكون الـ UPN غير مقروء أصلًا: «ما قريناهوش»
        //    ليست «مخالف»، وشارة مخالفة على وحدة سليمة أسوأ من غياب الشارة.
        public string? AdUserPrincipalName { get; set; }
        public bool UpnMatchesAccount { get; set; } = true;
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
        // ⚠️ عدّاد استثناء زي PendingSync: حساب الوحدة المفروض يفضل مُفعَّلًا.
        //    التعطيل بيحصل من الدليل بره النظام (إدارة الدومين أو الأمن
        //    السيبراني)، والنظام بيعرفه من كل مزامنة وبيخزّنه - وكان بيبان
        //    كشارة في الصفّ بس، يعني تلاقيه لو بصّيت على الصفّ ولا تعرف
        //    العدد ولا توصل لهم إلا بالتمرير على ٢٤٢ وحدة.
        public int AdDisabled { get; set; }
        // أرقام الأبراج الموجودة فعلًا — لملء قائمة الفلتر
        public List<int> Towers { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    // ---------- أرقام لوحة التحكم ----------
    // ⚠️ منفصل عن FacultyUnitsPageDto عن قصد: ده أربع أرقام للوحة التحكم،
    //    وده صفحة كاملة بعناصرها وفلاترها. لو اتشاركوا، أي إضافة لعدّاد في
    //    الشاشة كانت هتتحسب في اللوحة كمان بلا داعٍ.
    public class FacultyDashboardStatsDto
    {
        // ⚠️ الوحدات **في الخدمة** فقط لا كل الصفوف. الصفوف اللي حالتها
        //    OutOfService أو NotExists مش قابلة للتسكين، فلو دخلت في الإجمالي
        //    كانت «٧ شاغرة من ٤٨» هتبقى مضلّلة — المقام فيه وحدات مستحيل
        //    تتسكّن أصلًا.
        public int ActiveUnits { get; set; }
        public int Vacant { get; set; }
        // بيتعرض كسطر صغير تحت الإجمالي لما يبقى أكبر من صفر — عشان الرقم
        // اللي اتشال من المقام ما يختفيش من الشاشة خالص.
        public int OutOfService { get; set; }
        // ⚠️ عدّاد استثناء: المفروض يبقى صفر. Pending معناها اتسجّلت عندنا
        //    وما اتكتبتش في الدومين، وFailed معناها الدومين رفض. الاتنين
        //    محتاجين تدخّل، فبيتعدّوا مع بعض.
        public int PendingSync { get; set; }
    }

    // ---------- سجل الوحدة ----------
    public class OccupancyHistoryItemDto
    {
        public int Id { get; set; }
        public string FullNameAr { get; set; } = string.Empty;
        public string? FullNameEn { get; set; }
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
        // اختياري: الوحدات المستوردة قبل اعتماد displayName مالهاش اسم إنجليزي
        public string? FullNameEn { get; set; }
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
        // مسار الـ OU اللي الحساب اتنقل له في العملية دي — null يعني ما اتنقلش
        public string? MovedToOu { get; set; }
        public string? Error { get; set; }
        public FacultyUnitSyncState SyncState { get; set; }
    }
}
