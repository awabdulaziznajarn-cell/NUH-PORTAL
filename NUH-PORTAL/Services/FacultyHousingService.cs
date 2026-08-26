using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.FacultyHousing;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // إدارة سكن أعضاء هيئة التدريس: الوحدات، سجل الإشغال، والكتابة في الدومين.
    //
    // ⚠️ بيحقن AppDbContext مباشرة زي ADProvisioningService بالظبط — الخدمات
    //    اللي شغلها الأساسي تعامل مع الدومين ماشية على النمط ده في المشروع،
    //    وبتحتاج معاملات صريحة على أكتر من جدول في نفس اللحظة.
    public class FacultyHousingService
    {
        // ⚠️ الستّ خصائص دي هي كل اللي النظام بيكتبه في الدومين — لا أكتر.
        //    givenName و sn سايبينهم: القيمة بتاعتهم في وحدات السكن هي اسم
        //    الوحدة نفسها (villa08) مش اسم الساكن، وكتابة اسم الدكتور فيهم
        //    بتكسر أي حاجة بتعرف الوحدة من اسمها.
        //
        // ⚠️ و displayName كان معاهم في الاستثناء بنفس الحجّة، واتنقل هنا
        //    بقرار من إدارة النظام: المعتمَد في دليل الجامعة إن displayName
        //    يحمل اسم الساكن بالإنجليزي. فبقى مصدر الاسم الإنجليزي وقت
        //    الاستيراد، ووجهته عند الحفظ.
        // ⚠️ لكن الكتابة فيه مشروطة بوجود قيمة عندنا - انظر PushToAdAsync.
        //    القيمة الفاضية بتمسح الخاصية في الدليل، و ٢٤٢ وحدة مسجّلة قبل
        //    التغيير ده مالهاش اسم إنجليزي مخزَّن؛ فأول ضغطة «إعادة مزامنة»
        //    كانت هتفضّي displayName عند كل واحدة فيهم.
        public static readonly string[] ManagedAttributes =
            { "description", "displayName", "employeeID", "mobile", "company", "department" };

        // ⚠️ خصائص اسم الكائن — مطلوبة للنقل بين الأقسام لا للاستيراد.
        //    النقل في الدليل عملية ModifyDN بتلمس اسم الكائن، فمنح مقصور على
        //    الخمس خصائص فوق بيخلّي النقل يترفض بـ Access is denied بينما
        //    كل حاجة تانية شغّالة. اتكشفت من معاملة واقعة على الإنتاج.
        public static readonly string[] RdnAttributes = { "cn", "name" };

        private readonly AppDbContext _db;
        private readonly ActiveDirectoryService _ad;
        private readonly IUnitOfWork _uow;
        private readonly FacultyHousingConfig _config;
        private readonly IAuditService _audit;
        private readonly ILogger<FacultyHousingService> _logger;

        public FacultyHousingService(
            AppDbContext db,
            ActiveDirectoryService ad,
            IUnitOfWork uow,
            IOptions<FacultyHousingConfig> config,
            IAuditService audit,
            ILogger<FacultyHousingService> logger)
        {
            _db = db;
            _ad = ad;
            _uow = uow;
            _config = config.Value;
            _audit = audit;
            _logger = logger;
        }

        // =============================================================
        //  ١ · فحص الصلاحية
        // =============================================================
        // ⚠️ بيتنفّذ قبل أول استيراد. من غيره المستخدم بيضغط «استيراد» وبيستنى،
        //    وبعد دقيقة بيجيله خطأ مالوش معنى. الفحص ده بيقول بالظبط: أنهي OU
        //    بتتقرا، وأنهي خاصية بتتكتب، وعلى أنهي حساب اتجرّب.
        public async Task<AccessCheckDto> CheckAccessAsync()
        {
            var dto = new AccessCheckDto();
            var labelled = LabelledOus();
            dto.Configured = labelled.Count > 0;
            if (!dto.Configured) return dto;

            foreach (var (labelKey, ou) in labelled)
            {
                var res = await _ad.CheckOuAccessAsync(ou, ManagedAttributes, RdnAttributes);
                dto.Ous.Add(new OuAccessDto
                {
                    LabelKey = labelKey,
                    OuName = OuLeafName(ou),
                    OrganizationalUnit = ou,
                    OuExists = res.OuExists,
                    CanRead = res.CanRead,
                    AllWritable = res.AllWritable,
                    CanCreateUser = res.CanCreateUser,
                    CanWriteRdn = res.CanWriteRdn,
                    RdnAttributeWritable = new Dictionary<string, bool>(res.RdnAttributeWritable),
                    ProbedAccount = res.ProbedAccount,
                    AttributeWritable = new Dictionary<string, bool>(res.AttributeWritable),
                    Error = res.Error
                });
            }

            dto.AllOk = dto.Ous.Count > 0 && dto.Ous.All(o => o.CanRead && o.AllWritable);

            // ⚠️ الفحص ده على الأقسام المقسّمة بالجنس بس: هي الوحيدة اللي
            //    بيحصل نقل بينها. «الفلل» لو وحدة واحدة مختلطة مافيش نقل
            //    أصلًا، فصلاحية الإنشاء فيها مالهاش علاقة بالموضوع.
            var gendered = new[] { _config.TowersMaleOu, _config.TowersFemaleOu,
                                   _config.VillasMaleOu, _config.VillasFemaleOu }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var o in dto.Ous)
                o.IsGendered = gendered.Contains(o.OrganizationalUnit);

            var genderedRows = dto.Ous.Where(o => o.IsGendered).ToList();

            dto.HasGenderedOus = genderedRows.Count > 0;
            dto.MoveCreateOk = genderedRows.Count > 0 && genderedRows.All(o => o.CanCreateUser == true);
            dto.MoveRdnOk    = genderedRows.Count > 0 && genderedRows.All(o => o.CanWriteRdn);

            return dto;
        }

        // ⚠️ «أعضاء / عضوات هيئة التدريس» لا «الطلاب / الطالبات». الشاشة دي
        //    كلها سكن أعضاء هيئة التدريس، والأبراج دي مالهاش أي علاقة بسكن
        //    الطلاب - ده OU تاني خالص في الدومين وليه شاشته (إدارة حسابات
        //    الإسكان). الاسم الغلط كان بيخلّي اللي بيقرا الفحص يفتكر إنه بيبصّ
        //    على وحدات الطلاب، فيدوّر على مشكلة في المكان الغلط.
        //    ⚠️ MALE/FEMALE في اسم الـ OU معناها جنس **عضو هيئة التدريس**
        //       الساكن، مش مرحلة دراسية.
        // ⚠️ ما يعود هنا مفتاحُ ترجمة لا نصٌّ مقروء. كان النصّ العربي مكتوبًا
        //    في هذا الملف مباشرةً، فكان اسم الوحدة التنظيمية يظهر بالعربية
        //    حتى والبوابة تعمل بالإنجليزية، ولم يكن تعديله ممكنًا من ملفات
        //    الموارد — وهو أول مكان يقصده من أراد تغيير نصٍّ ظاهر.
        //    الترجمة تتمّ في الواجهة عبر FH_T، كما في بقية نصوص هذه الشاشة.
        private List<(string LabelKey, string Ou)> LabelledOus()
        {
            var list = new List<(string, string)>();
            if (!string.IsNullOrWhiteSpace(_config.TowersMaleOu)) list.Add(("fh_OuTowersMale", _config.TowersMaleOu));
            if (!string.IsNullOrWhiteSpace(_config.TowersFemaleOu)) list.Add(("fh_OuTowersFemale", _config.TowersFemaleOu));
            if (!string.IsNullOrWhiteSpace(_config.VillasOu)) list.Add(("fh_OuVillas", _config.VillasOu));
            if (!string.IsNullOrWhiteSpace(_config.VillasMaleOu)) list.Add(("fh_OuVillasMale", _config.VillasMaleOu!));
            if (!string.IsNullOrWhiteSpace(_config.VillasFemaleOu)) list.Add(("fh_OuVillasFemale", _config.VillasFemaleOu!));
            return list;
        }

        // ⚠️ الاسم التقني للوحدة التنظيمية كما هو في الدليل (MALE / FEMALE /
        //    Villas)، مُشتقًّا من المسار لا مكتوبًا بجانب الاسم العربي. لو
        //    كُتب بالإيد ثم غُيّرت الإعدادات، لبقي الاسم التقني يشير إلى قسم
        //    آخر — والشاشة تُقرأ حينها على أنها تصف الدليل وهي تصف نفسها.
        private static string OuLeafName(string? dn)
        {
            if (string.IsNullOrWhiteSpace(dn)) return string.Empty;
            var first = dn.Split(',')[0];
            var eq = first.IndexOf('=');
            return eq >= 0 && eq + 1 < first.Length ? first[(eq + 1)..].Trim() : first.Trim();
        }

        // =============================================================
        //  ٢ · الاستيراد — معاينة ثم تطبيق
        // =============================================================
        // ⚠️ المعاينة والتطبيق منفصلين عن قصد: الاستيراد بيلمس كل الوحدات مرة
        //    واحدة، ومافيش زرار تراجع. المستخدم لازم يشوف الجدول الأول.
        public async Task<ImportPreviewDto> PreviewImportAsync()
        {
            var dto = new ImportPreviewDto();
            var labelled = LabelledOus();
            if (labelled.Count == 0)
            {
                dto.Errors.Add("مسارات الوحدات التنظيمية غير مُعدّة في إعدادات النظام.");
                return dto;
            }

            var existing = await _db.FacultyUnits.AsNoTracking()
                .ToDictionaryAsync(u => u.AdAccount, StringComparer.OrdinalIgnoreCase);

            foreach (var (labelKey, ou) in labelled)
            {
                var ouName = OuLeafName(ou);
                var res = await _ad.SearchOuUsersAsync(ou, _config.MaxImportResults);
                if (!res.Success)
                {
                    // ⚠️ الاسم التقني لا مفتاح الترجمة: هذا السطر نصٌّ جاهز
                    //    يُعرض كما هو، ولا تمرّ عليه ترجمة الواجهة.
                    dto.Errors.Add($"{ouName}: {res.Error}");
                    continue;
                }
                if (res.Truncated) dto.Truncated = true;

                foreach (var u in res.Users)
                {
                    var parsed = FacultyAccountNaming.TryParse(u.SamAccountName);

                    // ⚠️ الحسابات اللي مش أسماء وحدات بتتعرض كـ «متجاهَل» مش
                    //    بتختفي. لو اختفت، حساب اتحط في الـ OU بالغلط هيفضل
                    //    مخفي للأبد ومحدش هيسأل عنه.
                    if (parsed == null)
                    {
                        dto.Rows.Add(new ImportRowDto
                        {
                            AdAccount = u.SamAccountName,
                            DistinguishedName = u.DistinguishedName,
                            OrganizationalUnitKey = labelKey,
                            OuName = ouName,
                            Action = ImportRowAction.Ignored,
                            DisplayName = u.SamAccountName,
                            Description = u.Description,
                            AdDisplayName = u.DisplayName,
                            AdUserPrincipalName = u.UserPrincipalName,
                            AccountEnabled = u.AccountEnabled,
                            IgnoreReasonKey = "fh_ImpIgnNameMismatch"
                        });
                        dto.IgnoredCount++;
                        continue;
                    }

                    existing.TryGetValue(u.SamAccountName, out var known);

                    // ⚠️ نفس الشروط اللي بتحدّد «Changed» بتتجمّع هنا - مش شرط
                    //    تاني منفصل. لو اتفارقوا، الجدول هيقول «سيتم تحديثها»
                    //    وسطر السبب يقول «لا تغيير».
                    // ⚠️ مفاتيح موارد لا نصوص عربية: الجدول ده بيتعرض في واجهة
                    //    ليها لغتان، ونصّ مكتوب هنا بيفضل عربي مهما اتبدّلت
                    //    اللغة، وتعديل صياغته بيتطلّب فتح ملف الخدمة بدل ملف
                    //    الموارد اللي كل النصوص التانية فيه.
                    var diffs = new List<string>();
                    if (known != null)
                    {
                        if (known.AdDistinguishedName != u.DistinguishedName)
                            diffs.Add("fh_ImpChgOuMoved");
                        if (known.AdAccountEnabled != u.AccountEnabled)
                            diffs.Add(u.AccountEnabled ? "fh_ImpChgAdEnabled" : "fh_ImpChgAdDisabled");
                        if (known.NameMatchesStandard != parsed.MatchesStandard)
                            diffs.Add(parsed.MatchesStandard
                                ? "fh_ImpChgNameOk"
                                : "fh_ImpChgNameBad");
                        // ⚠️ اسم الدخول اتغيّر في الدليل من برّه النظام. مش
                        //    خطأ يوقف الاستيراد - سطر في المعاينة بيقول
                        //    للمراجع إن الحساب اتلمس من مكان تاني.
                        // ⚠️ والشرط بيتخطّى المخزَّن الفاضي: أول استيراد بعد
                        //    إضافة العمود ده هيلاقيه فاضي في الـ ٢٤٢ وحدة كلها،
                        //    فالمقارنة المجرّدة كانت هتعلّم كل صفّ «تغيّر اسم
                        //    الدخول» - تحذير على ٢٤٢ وحدة سليمة معناه إن اللي
                        //    بيراجع يتعلّم يتجاهل التحذير من أول مرة. الفاضي
                        //    بيتملى بصمت، والاختلاف بعد كده هو اللي بيتعلّم.
                        if (!string.IsNullOrWhiteSpace(known.AdUserPrincipalName)
                            && !string.Equals(known.AdUserPrincipalName, u.UserPrincipalName ?? "",
                                              StringComparison.OrdinalIgnoreCase))
                            diffs.Add("fh_ImpChgUpn");
                    }

                    var action = known == null
                        ? ImportRowAction.New
                        : diffs.Count > 0 ? ImportRowAction.Changed : ImportRowAction.Unchanged;

                    dto.Rows.Add(new ImportRowDto
                    {
                        AdAccount = u.SamAccountName,
                        DistinguishedName = u.DistinguishedName,
                        OrganizationalUnitKey = labelKey,
                        OuName = ouName,
                        Action = action,
                        UnitType = parsed.UnitType,
                        TowerNo = parsed.TowerNo,
                        ApartmentNo = parsed.ApartmentNo,
                        VillaNo = parsed.VillaNo,
                        DisplayName = parsed.UnitType == FacultyUnitType.Villa
                            ? $"فيلا {parsed.VillaNo}"
                            : $"برج {parsed.TowerNo} - شقة {parsed.ApartmentNo}",
                        NameMatchesStandard = parsed.MatchesStandard,
                        Deviation = parsed.Deviation,
                        ChangeNoteKeys = diffs,
                        Description = u.Description,
                        AdDisplayName = u.DisplayName,
                        AdUserPrincipalName = u.UserPrincipalName,
                        EmployeeId = u.EmployeeId,
                        Mobile = u.Mobile,
                        Company = u.Company,
                        Department = u.Department,
                        AccountEnabled = u.AccountEnabled
                    });

                    if (action == ImportRowAction.New) dto.NewCount++;
                    else if (action == ImportRowAction.Changed) dto.ChangedCount++;
                    else dto.UnchangedCount++;

                    if (!parsed.MatchesStandard) dto.DeviationCount++;
                    if (!string.IsNullOrWhiteSpace(u.Description)) dto.WithOccupantCount++;
                }
            }

            dto.Rows = dto.Rows
                .OrderBy(r => r.Action == ImportRowAction.Ignored ? 1 : 0)
                .ThenBy(r => r.UnitType)
                .ThenBy(r => r.TowerNo ?? 0)
                .ThenBy(r => r.ApartmentNo ?? r.VillaNo ?? 0)
                .ToList();

            dto.Success = dto.Errors.Count == 0;
            return dto;
        }

        public async Task<ImportApplyResultDto> ApplyImportAsync()
        {
            var preview = await PreviewImportAsync();
            var result = new ImportApplyResultDto { Errors = preview.Errors };

            // ⚠️ مانكملش لو أي OU فشلت قراءتها. الاستيراد الجزئي أسوأ من عدمه:
            //    هيسجّل نص الوحدات وهيبان كأنه نجح، والباقي هيبان كأنه مش موجود.
            if (!preview.Success) return result;

            var now = DateTime.UtcNow;
            var actor = SafeUserId();

            var committed = false;
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in preview.Rows)
                {
                    if (row.Action == ImportRowAction.Ignored) continue;

                    var unit = await _db.FacultyUnits
                        .Include(u => u.Occupancies)
                        .FirstOrDefaultAsync(u => u.AdAccount == row.AdAccount);

                    if (unit == null)
                    {
                        unit = new FacultyUnit
                        {
                            AdAccount = row.AdAccount,
                            UnitType = row.UnitType ?? FacultyUnitType.Villa,
                            TowerNo = row.TowerNo,
                            ApartmentNo = row.ApartmentNo,
                            VillaNo = row.VillaNo,
                            Status = FacultyUnitStatus.Active,
                            CreatedAt = now,
                            CreatedBy = actor
                        };
                        _db.FacultyUnits.Add(unit);
                        result.UnitsCreated++;
                    }
                    else
                    {
                        unit.UpdatedAt = now;
                        unit.UpdatedBy = actor;
                        result.UnitsUpdated++;
                    }

                    unit.AdDistinguishedName = row.DistinguishedName;
                    unit.AdUserPrincipalName = NullIfBlank(row.AdUserPrincipalName);
                    unit.AdAccountEnabled = row.AccountEnabled;
                    unit.NameMatchesStandard = row.NameMatchesStandard;
                    unit.SyncState = FacultyUnitSyncState.Synced;
                    unit.LastSyncedAt = now;
                    unit.LastSyncError = null;

                    // ⚠️ صف الإشغال بيتفتح مرة واحدة بس: لو الوحدة عندها ساكن
                    //    مفتوح خلاص مانلمسهوش. إعادة تشغيل الاستيراد مالهاش
                    //    حق تقفل ساكن ولا تفتح صف جديد — دي إجراءات بقرار من
                    //    شاشة التسليم، والاستيراد مجرد قراءة.
                    // ⚠️ استثناء واحد على «الاستيراد مجرد قراءة»: الاسم
                    //    الإنجليزي بيتملى على الصفّ المفتوح **لو كان فاضي**.
                    //
                    //    السبب: الخانة دي اتضافت بعد ما الوحدات اتسجّلت، فهي
                    //    فاضية في كل صفّ مفتوح، ومصدرها الوحيد هو displayName
                    //    في الدليل. من غير المليان ده كان لازم موظف يفتح ٢٣٩
                    //    وحدة واحدة واحدة وينسخ الاسم من الدليل بإيده.
                    //
                    //  ⚠️ ومابيكتبش فوق قيمة موجودة أبدًا: لو موظف كتب الاسم
                    //     أو صحّحه، المكتوب عندنا هو المعتمَد - الاستيراد
                    //     بيملا الفاضي ولا بيراجع المليان. من غير الشرط ده كان
                    //     كل استيراد بيرجّع تصحيحات الموظفين للحالة القديمة.
                    var openRow = unit.Occupancies.FirstOrDefault(o => o.EndDate == null);
                    if (openRow != null
                        && string.IsNullOrWhiteSpace(openRow.FullNameEn)
                        && !string.IsNullOrWhiteSpace(row.AdDisplayName))
                    {
                        openRow.FullNameEn = row.AdDisplayName!.Trim();
                    }

                    var hasOpen = openRow != null;
                    if (!hasOpen && !string.IsNullOrWhiteSpace(row.Description))
                    {
                        _db.FacultyOccupancies.Add(new FacultyOccupancy
                        {
                            Unit = unit,
                            FullNameAr = row.Description!.Trim(),
                            // ⚠️ الاسم الإنجليزي من displayName زي ما هو في
                            //    الدليل. الفاضي بيفضل فاضي - مابنولّدش اسمًا
                            //    من العربي، لأن نقحرة مولّدة بتبان كأنها بيانات
                            //    موثّقة وهي تخمين.
                            FullNameEn = NullIfBlank(row.AdDisplayName),
                            NationalId = NullIfBlank(row.EmployeeId),
                            Mobile = NullIfBlank(row.Mobile),
                            College = NullIfBlank(row.Company),
                            Department = NullIfBlank(row.Department),
                            // ⚠️ تاريخ البداية = تاريخ الاستيراد، مش تاريخ إنشاء
                            //    الحساب. الدومين مايعرفش الساكن دخل إمتى، وحساب
                            //    الوحدة اتعمل من سنين ومر عليه أكتر من ساكن.
                            //    ImportedFromAd بتقول إن التاريخ ده تقديري.
                            StartDate = now,
                            ImportedFromAd = true,
                            CreatedAt = now,
                            CreatedBy = actor
                        });
                        result.OccupanciesOpened++;
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                committed = true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Faculty housing import failed");
                result.Errors.Add($"الاستيراد فشل ومافيش حاجة اتحفظت: {ex.Message}");
                result.UnitsCreated = result.UnitsUpdated = result.OccupanciesOpened = 0;
            }

            // ⚠️ خارج المعاملة عمدًا: لو كتبنا السجل داخل try بعد Commit ووقع
            //    خطأ في الكتابة، لَنَفَّذ catch أمر Rollback على معاملة مُثبَّتة
            //    بالفعل - فيتحوّل خطأ في التسجيل إلى استثناء يبتلع نجاح الاستيراد.
            //
            // ⚠️ والاستيراد يُنشئ وحدات ويفتح فترات إشغال بالجملة، وكان يمرّ بلا
            //    أثر. الأرقام وحدها تكفي: تقول متى استُورد وكم أُنشئ، فتفسّر
            //    ظهور عشرات السجلات الموسومة «مستورد» دفعة واحدة.
            if (committed)
            {
                await _audit.LogAsync("faculty_import_applied", "FacultyUnits", 0, new List<AuditChangeLog>
                {
                    new() { FieldName = "UnitsCreated",      OldValue = null, NewValue = result.UnitsCreated.ToString() },
                    new() { FieldName = "UnitsUpdated",      OldValue = null, NewValue = result.UnitsUpdated.ToString() },
                    new() { FieldName = "OccupanciesOpened", OldValue = null, NewValue = result.OccupanciesOpened.ToString() }
                });
                await _uow.SaveAsync();
            }

            return result;
        }

        // =============================================================
        //  ٣ · شاشة الوحدات
        // =============================================================
        public async Task<FacultyUnitsPageDto> GetUnitsAsync(
            string? type = null, string? status = null, string? search = null,
            bool onlyDeviations = false, bool onlyNeedsConfirm = false, bool onlyOuMismatch = false,
            bool onlyDisabled = false,
            int? tower = null, int page = 1, int pageSize = 50,
            string? sortBy = null, bool sortAsc = false)
        {
            var q = _db.FacultyUnits.AsNoTracking()
                .Select(u => new
                {
                    Unit = u,
                    Current = u.Occupancies.FirstOrDefault(o => o.EndDate == null)
                });

            if (!string.IsNullOrWhiteSpace(type))
            {
                var t = type.Trim().ToLowerInvariant();
                if (t == "tower") q = q.Where(x => x.Unit.UnitType == FacultyUnitType.Tower);
                else if (t == "villa") q = q.Where(x => x.Unit.UnitType == FacultyUnitType.Villa);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToLowerInvariant();
                if (s == "occupied") q = q.Where(x => x.Unit.Status == FacultyUnitStatus.Active && x.Current != null);
                else if (s == "vacant") q = q.Where(x => x.Unit.Status == FacultyUnitStatus.Active && x.Current == null);
                else if (s == "out_of_service") q = q.Where(x => x.Unit.Status == FacultyUnitStatus.OutOfService);
                else if (s == "not_exists") q = q.Where(x => x.Unit.Status == FacultyUnitStatus.NotExists);
                else if (s == "pending_sync") q = q.Where(x => x.Unit.SyncState != FacultyUnitSyncState.Synced);
            }

            if (onlyDeviations) q = q.Where(x => !x.Unit.NameMatchesStandard);

            // ⚠️ حسابات مُعطَّلة في الدليل. الشرط على العمود المخزَّن لا على
            //    قراءة حيّة: القيمة بتتحدّث مع كل مزامنة ومع كل كتابة، وقراءة
            //    ٢٤٢ حساب من الدليل عشان نفلتر جدول مالهاش معنى.
            if (onlyDisabled) q = q.Where(x => !x.Unit.AdAccountEnabled);

            // ============================================================
            //  فلتر «وحدة تنظيمية غير مطابقة».
            //
            //  ⚠️ الشرط ده هو **نفس** شرط CheckOuGender اللي بيرسم الشارة على
            //     الصف وبيحسب رقم الكارت - بس مكتوب بلغة SQL عشان الفلترة
            //     تحصل قبل التقسيم لصفحات. لو فلترنا في الذاكرة بعد Take(50)
            //     كنا هنفلتر الصفحة لا القائمة.
            //
            //  ⚠️ و«OU=MALE,» مش بتطابق «OU=FEMALE,» رغم إن MALE جوّه FEMALE:
            //     البادئة «OU=» هي اللي بتمنع ده. من غيرها كل وحدة بنات كانت
            //     هتتحسب وحدة بنين كمان.
            if (onlyOuMismatch)
                q = q.Where(x => x.Current != null && x.Unit.AdDistinguishedName != null &&
                    ((x.Unit.AdDistinguishedName.Contains("OU=MALE,") && x.Current.Gender == Gender.Female) ||
                     (x.Unit.AdDistinguishedName.Contains("OU=FEMALE,") && x.Current.Gender == Gender.Male)));

            // ⚠️ فلتر البرج بدل قائمة مسطّحة بـ٢٤٢ صف. القايمة الكاملة مالهاش
            //    معنى بصري: المستخدم بيدوّر على برج معيّن مش بيتصفّح الكل.
            if (tower.HasValue)
                q = q.Where(x => x.Unit.UnitType == FacultyUnitType.Tower && x.Unit.TowerNo == tower.Value);

            // ⚠️ «محتاجة تأكيد» = ساكن مفتوح عدّى عليه سنة من غير تأكيد. المقارنة
            //    على ConfirmedAt لو موجودة وإلا على StartDate — لأن الساكن
            //    المستورد مااتأكّدش أصلًا ولا مرة.
            var confirmCutoff = DateTime.UtcNow.AddMonths(-12);
            if (onlyNeedsConfirm)
                q = q.Where(x => x.Current != null
                                 && (x.Current.ConfirmedAt ?? x.Current.StartDate) < confirmCutoff);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();

                // ⚠️ اسم الوحدة «برج 1 - شقة 3» غير موجود في قاعدة البيانات: إنه
                //    محسوب في FacultyUnit.DisplayNameAr من TowerNo و ApartmentNo.
                //    فالبحث النصّي كان يفشل حتمًا على الاسم الذي يراه المستخدم
                //    مكتوبًا أمامه في العمود الأول - يبحث بالهوية فيجد، وبالاسم
                //    المعروض فلا يجد شيئًا، ولا سبب ظاهر للفرق.
                //    نُحلّل النصّ إلى أرقام ونطابقها على الأعمدة الحقيقية.
                var (pt, pa, pv, ptype) = ParseUnitSearch(s);

                if (pt.HasValue || pa.HasValue || pv.HasValue)
                {
                    q = q.Where(x =>
                        (pv.HasValue && x.Unit.UnitType == FacultyUnitType.Villa && x.Unit.VillaNo == pv)
                        || ((pt.HasValue || pa.HasValue)
                            && x.Unit.UnitType == FacultyUnitType.Tower
                            && (!pt.HasValue || x.Unit.TowerNo == pt)
                            && (!pa.HasValue || x.Unit.ApartmentNo == pa))
                        || x.Unit.AdAccount.Contains(s)
                        || (x.Current != null && x.Current.FullNameAr.Contains(s))
                        || (x.Current != null && x.Current.FullNameEn != null && x.Current.FullNameEn.Contains(s)));
                }
                else if (ptype.HasValue)
                {
                    // كلمة النوع وحدها بلا رقم - «فيلا» تعرض الفلل كلها
                    var wantedType = ptype.Value;
                    q = q.Where(x => x.Unit.UnitType == wantedType);
                }
                else
                {
                    // ⚠️ الاسم الإنجليزي داخل البحث: الموظف بيقرا السطرين في
                    //    العمود، فبحث بيلاقي السطر الأول ولا يلاقي التاني
                    //    بيبان كأن البحث بايظ.
                    q = q.Where(x => x.Unit.AdAccount.Contains(s)
                                     || (x.Current != null && x.Current.FullNameAr.Contains(s))
                                     || (x.Current != null && x.Current.FullNameEn != null && x.Current.FullNameEn.Contains(s))
                                     || (x.Current != null && x.Current.NationalId != null && x.Current.NationalId.Contains(s))
                                     || (x.Current != null && x.Current.Mobile != null && x.Current.Mobile.Contains(s)));
                }
            }

            var total = await q.CountAsync();

            // ⚠️ الترتيب على الخادم لا في المتصفح: الجدول مقسّم صفحات (٢٤٢ وحدة)،
            //    والترتيب في المتصفح بيرتّب الصفحة اللي قدامك بس — فأول اسم
            //    أبجديًّا في صفحة ٢ ممكن يسبق آخر اسم في صفحة ١.
            //
            // ⚠️ الافتراضي هو الترتيب الطبيعي للوحدات (نوع ← برج ← شقة/فيلا)،
            //    مش أبجدي ولا زمني: اللي بيفتح الشاشة بيدوّر على وحدة برقمها،
            //    والترتيب ده هو اللي بيخلّي «برج ٣ شقة ٢» جنب «برج ٣ شقة ٣».
            //
            // ⚠️ وبيانات الساكن كلها من x.Current اللي ممكن تكون null (وحدة
            //    شاغرة). الفحص الصريح != null مكتوب عشان الشرط يبان في الكود
            //    زي ما هو في قاعدة البيانات — الوحدات الشاغرة بتتجمّع في طرف
            //    واحد، وده الصح: هي فعلًا مجموعة واحدة.
            q = (sortBy?.ToLowerInvariant(), sortAsc) switch
            {
                ("unit", false)        => q.OrderByDescending(x => x.Unit.UnitType)
                                           .ThenByDescending(x => x.Unit.TowerNo ?? 0)
                                           .ThenByDescending(x => x.Unit.ApartmentNo ?? x.Unit.VillaNo ?? 0),
                ("ad_account", true)   => q.OrderBy(x => x.Unit.AdAccount),
                ("ad_account", false)  => q.OrderByDescending(x => x.Unit.AdAccount),
                ("occupant", true)     => q.OrderBy(x => x.Current != null ? x.Current.FullNameAr : null),
                ("occupant", false)    => q.OrderByDescending(x => x.Current != null ? x.Current.FullNameAr : null),
                ("national_id", true)  => q.OrderBy(x => x.Current != null ? x.Current.NationalId : null),
                ("national_id", false) => q.OrderByDescending(x => x.Current != null ? x.Current.NationalId : null),
                ("since", true)        => q.OrderBy(x => x.Current != null ? x.Current.StartDate : (DateTime?)null),
                ("since", false)       => q.OrderByDescending(x => x.Current != null ? x.Current.StartDate : (DateTime?)null),
                ("status", true)       => q.OrderBy(x => x.Unit.Status),
                ("status", false)      => q.OrderByDescending(x => x.Unit.Status),
                _                      => q.OrderBy(x => x.Unit.UnitType)
                                           .ThenBy(x => x.Unit.TowerNo ?? 0)
                                           .ThenBy(x => x.Unit.ApartmentNo ?? x.Unit.VillaNo ?? 0)
                                           .ThenBy(x => x.Unit.AdAccount)
            };

            var rows = await q
                .Skip(Math.Max(0, page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var nowUtc = DateTime.UtcNow;
            var dto = new FacultyUnitsPageDto
            {
                Total = total,
                Page = Math.Max(1, page),
                PageSize = pageSize,
                Items = rows.Select(x => new FacultyUnitListItemDto
                {
                    Id = x.Unit.Id,
                    AdAccount = x.Unit.AdAccount,
                    DisplayName = x.Unit.DisplayNameAr,
                    UnitType = x.Unit.UnitType,
                    TowerNo = x.Unit.TowerNo,
                    ApartmentNo = x.Unit.ApartmentNo,
                    VillaNo = x.Unit.VillaNo,
                    Status = x.Unit.Status,
                    SyncState = x.Unit.SyncState,
                    NameMatchesStandard = x.Unit.NameMatchesStandard,
                    AdAccountEnabled = x.Unit.AdAccountEnabled,
                    LastSyncError = x.Unit.LastSyncError,
                    OccupancyId = x.Current?.Id,
                    OccupantName = x.Current?.FullNameAr,
                    OccupantNameEn = x.Current?.FullNameEn,
                    OccupantNationalId = x.Current?.NationalId,
                    OccupantMobile = x.Current?.Mobile,
                    OccupantGender = x.Current?.Gender,
                    OccupantSince = x.Current?.StartDate,
                    OccupantImported = x.Current?.ImportedFromAd ?? false,
                    ConfirmedAt = x.Current?.ConfirmedAt,
                    MonthsSinceConfirmed = x.Current == null
                        ? null
                        : (int)((nowUtc - (x.Current.ConfirmedAt ?? x.Current.StartDate)).TotalDays / 30),
                    OuGenderMismatch = CheckOuGender(x.Unit.AdDistinguishedName, x.Current?.Gender).Mismatch,
                    OuGenderMismatchNote = CheckOuGender(x.Unit.AdDistinguishedName, x.Current?.Gender).Note,
                    AdUserPrincipalName = x.Unit.AdUserPrincipalName,
                    UpnMatchesAccount = x.Unit.UpnMatchesAccount
                }).ToList()
            };

            // أرقام الأبراج الموجودة فعلًا — بتملا قائمة الفلتر بدل ما نفترض ١..١٣
            dto.Towers = await _db.FacultyUnits.AsNoTracking()
                .Where(u => u.UnitType == FacultyUnitType.Tower && u.TowerNo != null)
                .Select(u => u.TowerNo!.Value).Distinct().OrderBy(n => n).ToListAsync();

            await FillCountersAsync(dto, confirmCutoff);
            return dto;
        }

        // =============================================================
        //  أرقام لوحة التحكم — أربعة عدّادات لا أكتر
        // =============================================================
        // ⚠️ ليه منفصلة عن FillCountersAsync: دي بتعمل عشر استعلامات وواحد
        //    منهم بيقرا صفوفًا ويحسب في الذاكرة (OuMismatches). لوحة التحكم
        //    بتتفتح مع كل دخول، فما ينفعش تدفع تمن عدّادات شاشة مالهاش دعوة
        //    بيها. الأربعة دول أربع CountAsync بسيطة.
        // ⚠️ والتعريفات هي **نفسها** المستعملة في شاشة الوحدات بالحرف
        //    (Active + إشغال مفتوح/مقفول)، فالرقم في اللوحة والرقم في الشاشة
        //    مايفارقوش.
        public async Task<FacultyDashboardStatsDto> GetDashboardStatsAsync()
        {
            var occupied = await _db.FacultyUnits.CountAsync(u =>
                u.Status == FacultyUnitStatus.Active && u.Occupancies.Any(o => o.EndDate == null));
            var vacant = await _db.FacultyUnits.CountAsync(u =>
                u.Status == FacultyUnitStatus.Active && !u.Occupancies.Any(o => o.EndDate == null));

            return new FacultyDashboardStatsDto
            {
                // الوحدة في الخدمة إما مشغولة أو شاغرة — مفيش حالة تالتة،
                // فالمجموع هو عدد الوحدات القابلة للتسكين بلا استعلام زيادة.
                ActiveUnits = occupied + vacant,
                Vacant = vacant,
                OutOfService = await _db.FacultyUnits.CountAsync(u => u.Status == FacultyUnitStatus.OutOfService),
                PendingSync = await _db.FacultyUnits.CountAsync(u => u.SyncState != FacultyUnitSyncState.Synced)
            };
        }

        private async Task FillCountersAsync(FacultyUnitsPageDto dto, DateTime confirmCutoff)
        {
            // ⚠️ العدّادات على الجدول كله مش على الصفحة المعروضة. عدّاد بيتغيّر
            //    مع الفلتر بيخلّي «إجمالي الوحدات» يقول رقم مختلف كل شاشة.
            dto.TotalUnits = await _db.FacultyUnits.CountAsync();
            dto.OutOfService = await _db.FacultyUnits.CountAsync(u => u.Status == FacultyUnitStatus.OutOfService);
            dto.NotExists = await _db.FacultyUnits.CountAsync(u => u.Status == FacultyUnitStatus.NotExists);
            dto.PendingSync = await _db.FacultyUnits.CountAsync(u => u.SyncState != FacultyUnitSyncState.Synced);
            dto.NameDeviations = await _db.FacultyUnits.CountAsync(u => !u.NameMatchesStandard);
            dto.AdDisabled = await _db.FacultyUnits.CountAsync(u => !u.AdAccountEnabled);

            dto.Occupied = await _db.FacultyUnits.CountAsync(u =>
                u.Status == FacultyUnitStatus.Active && u.Occupancies.Any(o => o.EndDate == null));
            dto.Vacant = await _db.FacultyUnits.CountAsync(u =>
                u.Status == FacultyUnitStatus.Active && !u.Occupancies.Any(o => o.EndDate == null));
            dto.NeedsConfirm = await _db.FacultyUnits.CountAsync(u =>
                u.Occupancies.Any(o => o.EndDate == null
                                       && (o.ConfirmedAt ?? o.StartDate) < confirmCutoff));

            // ⚠️ العدّاد ده بيتحسب في الذاكرة مش في SQL: المقارنة على نص الـ DN
            //    ومحتاجة منطق مش قابل للترجمة لاستعلام. بنجيب الحد الأدنى — الـ DN
            //    وجنس الساكن المفتوح بس — مش الصفوف كاملة.
            var genderRows = await _db.FacultyUnits.AsNoTracking()
                .Where(u => u.AdDistinguishedName != null)
                .Select(u => new
                {
                    u.AdDistinguishedName,
                    Gender = u.Occupancies.Where(o => o.EndDate == null).Select(o => o.Gender).FirstOrDefault()
                })
                .ToListAsync();
            dto.OuMismatches = genderRows.Count(r => CheckOuGender(r.AdDistinguishedName, r.Gender).Mismatch);
        }

        // =============================================================
        //  ٤ · سجل الوحدة
        // =============================================================
        public async Task<FacultyUnitDetailDto> GetUnitDetailAsync(int unitId)
        {
            var unit = await _db.FacultyUnits.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == unitId)
                ?? throw new UserFriendlyException("الوحدة غير موجودة", 404);

            var history = await _db.FacultyOccupancies.AsNoTracking()
                .Include(o => o.CreatedByUser)
                .Include(o => o.ClosedByUser)
                .Where(o => o.UnitId == unitId)
                .OrderByDescending(o => o.EndDate == null)
                .ThenByDescending(o => o.StartDate)
                .ToListAsync();

            var current = history.FirstOrDefault(o => o.EndDate == null);
            var nowUtc = DateTime.UtcNow;

            // ====================================================================
            //  سجل العمليات على هذه الوحدة.
            //
            //  ⚠️ الترقيم على مرجعين لا مرجع واحد عمدًا: التصحيح كان - ولا يزال -
            //     يُقيَّد على *فترة الإشغال* (FacultyOccupancies)، والتسليم والكتابة
            //     في الدومين تُقيَّد على *الوحدة* (FacultyUnits). لو قرأنا مرجعًا
            //     واحدًا لسقط نصف السجل، ولو غيّرنا الترقيم القديم لضاعت السجلات
            //     المكتوبة فعلًا في قاعدة البيانات. فنقرأ الاثنين.
            // ====================================================================
            var occIds = history.Select(o => o.Id).ToList();
            var auditRows = await _db.AuditLogs.AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.AuditChangeLogs)
                .Where(a => (a.target_table == "FacultyUnits" && a.target_id == unitId)
                         || (a.target_table == "FacultyOccupancies" && occIds.Contains(a.target_id)))
                .OrderByDescending(a => a.action_at)
                .Take(200)
                .ToListAsync();

            return new FacultyUnitDetailDto
            {
                Changes = auditRows.Select(a => new FacultyAuditItemDto
                {
                    Id = a.Id,
                    Action = a.action ?? "",
                    ActionAt = a.action_at,
                    ActorName = a.User == null ? null : (a.User.full_name ?? a.User.UserName),
                    IpAddress = a.ip_address,
                    Fields = (a.AuditChangeLogs ?? new List<AuditChangeLog>())
                        .Select(c => new FacultyAuditFieldDto
                        {
                            FieldName = c.FieldName,
                            OldValue = c.OldValue,
                            NewValue = c.NewValue
                        }).ToList()
                }).ToList(),
                DistinguishedName = unit.AdDistinguishedName,
                Notes = unit.Notes,
                LastSyncedAt = unit.LastSyncedAt,
                Unit = new FacultyUnitListItemDto
                {
                    Id = unit.Id,
                    AdAccount = unit.AdAccount,
                    DisplayName = unit.DisplayNameAr,
                    UnitType = unit.UnitType,
                    TowerNo = unit.TowerNo,
                    ApartmentNo = unit.ApartmentNo,
                    VillaNo = unit.VillaNo,
                    Status = unit.Status,
                    SyncState = unit.SyncState,
                    NameMatchesStandard = unit.NameMatchesStandard,
                    AdAccountEnabled = unit.AdAccountEnabled,
                    LastSyncError = unit.LastSyncError,
                    OccupancyId = current?.Id,
                    OccupantName = current?.FullNameAr,
                    OccupantNameEn = current?.FullNameEn,
                    OccupantNationalId = current?.NationalId,
                    OccupantMobile = current?.Mobile,
                    OccupantGender = current?.Gender,
                    OccupantSince = current?.StartDate,
                    OccupantImported = current?.ImportedFromAd ?? false,
                    ConfirmedAt = current?.ConfirmedAt,
                    MonthsSinceConfirmed = current == null
                        ? null
                        : (int)((nowUtc - (current.ConfirmedAt ?? current.StartDate)).TotalDays / 30),
                    OuGenderMismatch = CheckOuGender(unit.AdDistinguishedName, current?.Gender).Mismatch,
                    OuGenderMismatchNote = CheckOuGender(unit.AdDistinguishedName, current?.Gender).Note,
                    AdUserPrincipalName = unit.AdUserPrincipalName,
                    UpnMatchesAccount = unit.UpnMatchesAccount
                },
                History = history.Select(o => new OccupancyHistoryItemDto
                {
                    Id = o.Id,
                    FullNameAr = o.FullNameAr,
                    FullNameEn = o.FullNameEn,
                    Gender = o.Gender,
                    NationalId = o.NationalId,
                    Mobile = o.Mobile,
                    College = o.College,
                    Department = o.Department,
                    StartDate = o.StartDate,
                    EndDate = o.EndDate,
                    EndReason = o.EndReason,
                    EndReasonNote = o.EndReasonNote,
                    TicketNo = o.TicketNo,
                    CyberApprovedAt = o.CyberApprovedAt,
                    OriginalFileName = o.OriginalFileName,
                    ImportedFromAd = o.ImportedFromAd,
                    IsCurrent = o.EndDate == null,
                    ConfirmedAt = o.ConfirmedAt,
                    CreatedByName = o.CreatedByUser == null ? null : o.CreatedByUser.full_name,
                    ClosedByName = o.ClosedByUser == null ? null : o.ClosedByUser.full_name
                }).ToList()
            };
        }

        // =============================================================
        //  ٥ · تسليم وحدة لساكن جديد
        // =============================================================
        // ⚠️ الإجراء ده هو سبب المشروع كله. قبله كان تغيير الساكن = كتابة فوق
        //    description في الدومين، والاسم القديم بيتمسح للأبد.
        //    هنا: بنقفل صف الساكن القديم بتاريخ، وبنفتح صف جديد، وبنكتب في
        //    الدومين. التلاتة في معاملة واحدة — يا يتم كله يا مايحصلش حاجة.
        //    ⚠️ الصف القديم مايتعدلش، بيتقفل بس. لو عدّلناه في مكانه نبقى رجعنا
        //    لنفس مشكلة الدومين بالظبط.
        public async Task<AdPushResultDto> HandoverAsync(HandoverRequestDto dto)
        {
            var unit = await _db.FacultyUnits
                .Include(u => u.Occupancies)
                .FirstOrDefaultAsync(u => u.Id == dto.UnitId)
                ?? throw new UserFriendlyException("الوحدة غير موجودة", 404);

            var ticket = ValidateTicketNo(dto.TicketNo);

            var now = DateTime.UtcNow;
            var actor = SafeUserId();
            var current = unit.Occupancies.FirstOrDefault(o => o.EndDate == null);
            var closing = dto.RequestType != FacultyRequestType.NewService;
            var opening = dto.RequestType != FacultyRequestType.StopService;

            // ⚠️ «سبب آخر» لازم معاه بيان، والأسباب التانية مايتخزنش معاها بيان.
            //    الواجهة بتقفل الخانة وبتمسحها، بس الـ API نفسه مفتوح لأي
            //    عميل - والقاعدة اللي في الواجهة بس مش قاعدة.
            if (closing)
            {
                if (dto.EndReason == OccupancyEndReason.Other && string.IsNullOrWhiteSpace(dto.EndReasonNote))
                    throw new UserFriendlyException("بيان السبب مطلوب عند اختيار «سبب آخر»", 400);

                // الأسباب التلاتة التانية بتشرح نفسها - أي بيان معاها بيتشال
                // عشان مايبقاش عندنا صفوف سببها واضح وبيانها بيقول حاجة تانية.
                if (dto.EndReason != OccupancyEndReason.Other)
                    dto.EndReasonNote = null;
            }

            if (opening)
            {
                if (string.IsNullOrWhiteSpace(dto.FullNameAr))
                    throw new UserFriendlyException("اسم الشاغل الجديد مطلوب", 400);

                dto.NationalId = ValidateNationalId(dto.NationalId);
                dto.Mobile = NormalizeMobile(dto.Mobile);
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (closing && current != null)
                {
                    current.EndDate = dto.EndDate ?? now;
                    current.EndReason = dto.EndReason ?? OccupancyEndReason.ContractEnded;
                    current.EndReasonNote = NullIfBlank(dto.EndReasonNote);
                    current.ClosedBy = actor;
                    // رقم التذكرة بيتسجّل على الصفين: اللي اتقفل واللي اتفتح.
                    // الإجراء واحد، فلازم يبان في تاريخ الاتنين.
                    if (string.IsNullOrWhiteSpace(current.TicketNo)) current.TicketNo = ticket;
                }

                if (opening)
                {
                    // ⚠️ الفهرس UX_FacultyOccupancies_open هيرفض أي صف تاني مفتوح
                    //    على نفس الوحدة. بنفحص هنا كمان عشان الرسالة تبقى مفهومة
                    //    بدل ما المستخدم يشوف خطأ قاعدة بيانات خام.
                    if (!closing && current != null)
                        throw new UserFriendlyException("الوحدة مسجّل عليها شاغل حالي. اختر «تغيير الشاغل» بدلاً من «خدمة جديدة».", 400);

                    _db.FacultyOccupancies.Add(new FacultyOccupancy
                    {
                        UnitId = unit.Id,
                        FullNameAr = dto.FullNameAr!.Trim(),
                        FullNameEn = NullIfBlank(dto.FullNameEn),
                        Gender = dto.Gender,
                        NationalId = dto.NationalId,
                        Mobile = dto.Mobile,
                        College = NullIfBlank(dto.College),
                        Department = NullIfBlank(dto.Department),
                        StartDate = dto.StartDate ?? now,
                        TicketNo = ticket,
                        CyberApprovedAt = dto.CyberApprovedAt,
                        ImportedFromAd = false,
                        ConfirmedAt = now,
                        ConfirmedBy = actor,
                        CreatedAt = now,
                        CreatedBy = actor
                    });
                }

                // ⚠️ الحالة بتتعلّم Pending قبل الكتابة مش بعدها. لو التطبيق وقع
                //    في نص الكتابة، الوحدة بتفضل «في انتظار المزامنة» — أصدق من
                //    إنها تفضل «متزامنة» وهي مش كده.
                unit.SyncState = FacultyUnitSyncState.Pending;
                unit.UpdatedAt = now;
                unit.UpdatedBy = actor;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (UserFriendlyException) { await tx.RollbackAsync(); throw; }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Handover failed for unit {Id}", dto.UnitId);
                throw new UserFriendlyException("تعذّر إتمام التسليم، ولم يُحفظ أي تغيير: " + ex.Message, 500);
            }

            // ====================================================================
            //  ⚠️ التسليم يُقيَّد في سجل العمليات كما يُقيَّد التصحيح.
            //
            //     كان بلا أثر إطلاقًا، وهو أخطر إجراء في الميزة: شخص يُسجَّل أنه
            //     غادر وآخر يُسجَّل أنه سكن، ويُعاد كتابة حساب في الدليل النشط -
            //     ولا سبيل لمعرفة من فعل ذلك ولا متى. سجل الإشغال نفسه يحفظ
            //     النتيجة لا الإجراء: يقول «فلان غادر» ولا يقول «فلانٌ سجّل ذلك».
            //
            //     الترقيم على الوحدة لا على فترة الإشغال، لأن الإجراء يمسّ فترتين
            //     معًا - المُغلقة والمفتوحة - فلا تُنسب إلى إحداهما دون الأخرى.
            // ====================================================================
            var hoChanges = new List<AuditChangeLog>
            {
                new() { FieldName = "TicketNo", OldValue = null, NewValue = ticket }
            };
            if (closing && current != null)
            {
                // الاسم السابق يُقيَّد وحده عند «إيقاف الخدمة» فقط: في «تغيير
                // الشاغل» يظهر في خانة «قبل» أمام الاسم الجديد، فلا يُكرَّر.
                if (!opening)
                    hoChanges.Add(new AuditChangeLog { FieldName = "PreviousOccupant", OldValue = current.FullNameAr, NewValue = null });

                hoChanges.Add(new AuditChangeLog
                {
                    FieldName = "EndReason",
                    OldValue = null,
                    NewValue = (current.EndReason?.ToString() ?? "") +
                               (string.IsNullOrWhiteSpace(current.EndReasonNote) ? "" : " - " + current.EndReasonNote)
                });
            }
            if (opening)
            {
                hoChanges.Add(new AuditChangeLog { FieldName = "FullNameAr",  OldValue = current?.FullNameAr, NewValue = dto.FullNameAr!.Trim() });
                hoChanges.Add(new AuditChangeLog { FieldName = "FullNameEn",  OldValue = current?.FullNameEn, NewValue = NullIfBlank(dto.FullNameEn) });
                hoChanges.Add(new AuditChangeLog { FieldName = "NationalId",  OldValue = current?.NationalId, NewValue = dto.NationalId });
                hoChanges.Add(new AuditChangeLog { FieldName = "Mobile",      OldValue = current?.Mobile,     NewValue = dto.Mobile });
                hoChanges.Add(new AuditChangeLog { FieldName = "College",     OldValue = current?.College,    NewValue = NullIfBlank(dto.College) });
                hoChanges.Add(new AuditChangeLog { FieldName = "Department",  OldValue = current?.Department, NewValue = NullIfBlank(dto.Department) });
            }

            var hoAction = dto.RequestType switch
            {
                FacultyRequestType.NewService  => "faculty_service_started",
                FacultyRequestType.StopService => "faculty_service_stopped",
                _                              => "faculty_handover"
            };
            await _audit.LogAsync(hoAction, "FacultyUnits", unit.Id, hoChanges);
            await _uow.SaveAsync();

            // الكتابة في الدومين بره المعاملة عن قصد: عملية شبكة ممكن تاخد وقت،
            // ومانسيبش معاملة قاعدة بيانات مفتوحة مستنية رد من سيرفر تاني.
            if (dto.PushToAd) return await PushToAdAsync(unit.Id);

            return new AdPushResultDto { Success = true, SyncState = FacultyUnitSyncState.Pending };
        }

        // =============================================================
        //  ٥-ب · تصحيح بيانات الساكن الحالي
        // =============================================================
        // ⚠️ التصحيح غير التسليم: الشخص نفسه لم يتغيّر، وإنما بياناته مسجّلة
        //    خطأ. فلا يُفتح سجل إشغال جديد، بل يُعدَّل السجل المفتوح في مكانه.
        //    لو فتحنا سجلًا جديدًا لصار في تاريخ الوحدة «فلان غادر وفلان سكن»
        //    في اليوم نفسه - انتقال لم يحدث أصلًا.
        //
        // ⚠️ والسجل المُغلق يبقى غير قابل للتعديل كما هو: هذه الدالة لا تمسّ
        //    إلا السجل المفتوح، وهو وحده الذي يصف الحاضر.
        //
        // ⚠️ كل تعديل يُقيَّد في سجل العمليات بقيمته القديمة والجديدة. تعديل
        //    رقم هوية أو جوال بلا أثر يعني أن أحدًا غيّر بيانات ولا سبيل لمعرفة
        //    من ولا متى - وهذه بيانات شخصية تُكتب في الدليل.
        public async Task<AdPushResultDto> UpdateOccupantAsync(int unitId, HandoverRequestDto dto)
        {
            var unit = await _db.FacultyUnits
                .Include(u => u.Occupancies)
                .FirstOrDefaultAsync(u => u.Id == unitId)
                ?? throw new UserFriendlyException("الوحدة غير موجودة", 404);

            var current = unit.Occupancies.FirstOrDefault(o => o.EndDate == null)
                ?? throw new UserFriendlyException("لا يوجد ساكن حالي لهذه الوحدة. استخدم «تغيير الساكن» لتسجيل ساكن جديد.", 400);

            if (string.IsNullOrWhiteSpace(dto.FullNameAr))
                throw new UserFriendlyException("اسم عضو هيئة التدريس مطلوب", 400);

            var newName = dto.FullNameAr.Trim();
            var newNameEn = NullIfBlank(dto.FullNameEn);
            var newNid = ValidateNationalId(dto.NationalId);
            var newMobile = NormalizeMobile(dto.Mobile);
            var newCollege = NullIfBlank(dto.College);
            var newDept = NullIfBlank(dto.Department);

            // ⚠️ تُرصد الفروق قبل الكتابة: بعدها تكون القيمة القديمة قد ضاعت.
            var changes = new List<AuditChangeLog>();
            void Track(string field, string? oldVal, string? newVal)
            {
                if (string.Equals(oldVal ?? "", newVal ?? "", StringComparison.Ordinal)) return;
                changes.Add(new AuditChangeLog { FieldName = field, OldValue = oldVal, NewValue = newVal });
            }

            Track("FullNameAr", current.FullNameAr, newName);
            Track("FullNameEn", current.FullNameEn, newNameEn);
            Track("NationalId", current.NationalId, newNid);
            Track("Mobile", current.Mobile, newMobile);
            Track("College", current.College, newCollege);
            Track("Department", current.Department, newDept);
            Track("Gender", current.Gender?.ToString(), dto.Gender?.ToString());
            Track("StartDate", current.StartDate.ToString("yyyy-MM-dd"),
                  (dto.StartDate ?? current.StartDate).ToString("yyyy-MM-dd"));

            // لا شيء تغيّر: لا نكتب سجل عمليات فارغًا ولا نُعلّم الوحدة للمزامنة
            if (changes.Count == 0)
                return new AdPushResultDto { Success = true, SyncState = unit.SyncState };

            current.FullNameAr = newName;
            current.FullNameEn = newNameEn;
            current.NationalId = newNid;
            current.Mobile = newMobile;
            current.College = newCollege;
            current.Department = newDept;
            current.Gender = dto.Gender ?? current.Gender;
            if (dto.StartDate.HasValue) current.StartDate = dto.StartDate.Value;

            // ⚠️ التصحيح يُلغي وسم «مستورد»: التاريخ صار مُراجَعًا من موظف لا
            //    مُستنتَجًا من تاريخ الاستيراد، فلا يصحّ أن يظل موسومًا بالتقدير.
            current.ImportedFromAd = false;
            current.ConfirmedAt = DateTime.UtcNow;
            current.ConfirmedBy = SafeUserId();

            unit.SyncState = FacultyUnitSyncState.Pending;
            unit.UpdatedAt = DateTime.UtcNow;
            unit.UpdatedBy = SafeUserId();

            await _db.SaveChangesAsync();
            await _audit.LogAsync("faculty_occupant_updated", "FacultyOccupancies", current.Id, changes);
            await _uow.SaveAsync();

            if (dto.PushToAd) return await PushToAdAsync(unit.Id);
            return new AdPushResultDto { Success = true, SyncState = FacultyUnitSyncState.Pending };
        }

        // =============================================================
        //  ٦ · الفرق والكتابة في الدومين
        // =============================================================
        public async Task<AdDiffDto> BuildAdDiffAsync(int unitId)
        {
            var unit = await _db.FacultyUnits.AsNoTracking()
                .Include(u => u.Occupancies)
                .FirstOrDefaultAsync(u => u.Id == unitId)
                ?? throw new UserFriendlyException("الوحدة غير موجودة", 404);

            var dto = new AdDiffDto { UnitId = unit.Id, AdAccount = unit.AdAccount, DistinguishedName = unit.AdDistinguishedName };
            var current = unit.Occupancies.FirstOrDefault(o => o.EndDate == null);

            var read = await _ad.GetUserBySamAccountNameAsync(unit.AdAccount);
            if (!read.Success)
            {
                dto.Error = read.Error;
                return dto;
            }
            dto.AccountFound = true;
            dto.DistinguishedName = read.DistinguishedName ?? unit.AdDistinguishedName;

            void Line(string attr, string? cur, string? next)
            {
                var willChange = !string.Equals(cur ?? "", next ?? "", StringComparison.Ordinal);
                dto.Lines.Add(new AdDiffLineDto { Attribute = attr, CurrentValue = cur, NewValue = next, WillChange = willChange });
                if (willChange) dto.ChangeCount++;
            }

            Line("description", read.Description, current?.FullNameAr);
            Line("employeeID", read.EmployeeId, current?.NationalId);
            Line("mobile", read.Mobile, current?.Mobile);
            Line("company", read.Company, current?.College);
            Line("department", read.Department, current?.Department);

            // ⚠️ ‏displayName بقى خاصية مُدارة (الاسم الإنجليزي للساكن)، لكن
            //    سطر الفرق بيقول الحقيقة كاملة: لو مافيش اسم إنجليزي مسجَّل
            //    عندنا فالكتابة بتتخطّاه ولا بتفضّيه. غير كده كان السطر هيقول
            //    «هيتغيّر من فلان إلى (فارغ)» وهو مش هيحصل - ولوحة الفرق دي
            //    كل قيمتها إنها تطابق اللي هيتنفّذ.
            if (current == null || string.IsNullOrWhiteSpace(current.FullNameEn))
            {
                dto.Lines.Add(new AdDiffLineDto
                {
                    Attribute = "displayName",
                    CurrentValue = read.DisplayName,
                    NewValue = read.DisplayName,
                    WillChange = false,
                    Note = "لا يتغيّر - لا يوجد اسم إنجليزي مسجَّل"
                });
            }
            else
            {
                Line("displayName", read.DisplayName, current.FullNameEn);
            }

            // ⚠️ اسم الدخول للعرض فقط: النظام مابيكتبش فيه. وبيتعرض عشان
            //    اختلاف مقدّمته عن اسم الحساب معلومة بتفسّر أعطال دخول،
            //    ومحدش كان بيشوفها إلا من داخل الدليل نفسه.
            var upnLocal = (read.UserPrincipalName ?? "").Split('@')[0];
            dto.Lines.Add(new AdDiffLineDto
            {
                Attribute = "userPrincipalName",
                CurrentValue = read.UserPrincipalName,
                NewValue = read.UserPrincipalName,
                WillChange = false,
                Note = string.IsNullOrWhiteSpace(read.UserPrincipalName)
                    ? "لا يتغيّر - للعرض فقط"
                    : (string.Equals(upnLocal, unit.AdAccount, StringComparison.OrdinalIgnoreCase)
                        ? "لا يتغيّر - للعرض فقط"
                        : "لا يتغيّر - ومقدّمته تخالف اسم الحساب")
            });

            // ⚠️ النقل بين الـ OU بيبان في الفرق زي أي تغيير تاني، وقبل الكتابة.
            //    ده مش تعديل خانة - ده نقل كائن في الدليل بيغيّر السياسات
            //    والصلاحيات المطبّقة عليه. اللي بيدوس «كتابة» لازم يكون شايفه.
            var move = ResolveOuMove(unit, current?.Gender, dto.DistinguishedName);
            if (move.Needed)
            {
                dto.Lines.Add(new AdDiffLineDto
                {
                    Attribute = "OU",
                    CurrentValue = ParentOu(dto.DistinguishedName),
                    NewValue = move.TargetOu,
                    WillChange = true,
                    Note = move.Note
                });
                dto.ChangeCount++;
            }
            else if (move.Note != null)
            {
                dto.Lines.Add(new AdDiffLineDto
                {
                    Attribute = "OU",
                    CurrentValue = ParentOu(dto.DistinguishedName),
                    NewValue = ParentOu(dto.DistinguishedName),
                    WillChange = false,
                    Note = move.Note
                });
            }

            return dto;
        }

        public async Task<AdPushResultDto> PushToAdAsync(int unitId)
        {
            var unit = await _db.FacultyUnits
                .Include(u => u.Occupancies)
                .FirstOrDefaultAsync(u => u.Id == unitId)
                ?? throw new UserFriendlyException("الوحدة غير موجودة", 404);

            var current = unit.Occupancies.FirstOrDefault(o => o.EndDate == null);

            // ============================================================
            //  الـ DN بيتقرا من الدومين **في كل مرة** لا بيتاخد من المحفوظ.
            //
            //  ⚠️ الشرط هنا كان `if (string.IsNullOrWhiteSpace(dn))` - يعني
            //     بنقرا من الدومين لما يكون المحفوظ **فاضي** بس. والتعليق
            //     اللي كان فوقه بيقول السبب الصح («الـ DN ممكن يكون اتغيّر لو
            //     الحساب اتنقل لـ OU تانية») - بس الشرط كان بيعمل العكس:
            //     الحالة الوحيدة اللي الـ DN مايكونش فيها بايت هي لما يكون
            //     فاضي أصلًا.
            //
            //     النتيجة اللي حصلت فعلًا: الحساب اتنقل (بإيدنا أو بإيد إدارة
            //     الدومين)، والمحفوظ عندنا فضل على المسار القديم، فالكتابة
            //     رجعت «The object does not exist ... best match: OU=MALE».
            //
            //  ⚠️ الـ DN بيانات **الدليل** بيملكها لا إحنا: أي حد يحرّك الحساب
            //     من Active Directory Users and Computers بيبطّل المحفوظ عندنا
            //     من غير ما يعدّي علينا. فاسم الحساب (sAMAccountName) هو
            //     المعرّف الثابت، والمسار قيمة بتتقرا وقت الاستعمال.
            //
            //  ⚠️ وde قراءة واحدة زيادة لكل كتابة - وشاشة الفرق بتعملها أصلًا
            //     (BuildAdDiffAsync). كانت الشاشة بتقرا الواقع والكتابة بتشتغل
            //     على المحفوظ، فالمستخدم يشوف فرقًا صح وتفشل الكتابة اللي بعده.
            // ============================================================
            var read = await _ad.GetUserBySamAccountNameAsync(unit.AdAccount);
            if (!read.Success || string.IsNullOrWhiteSpace(read.DistinguishedName))
                return await FailSync(unit, read.Error ?? "الحساب غير موجود في الـAD");

            var dn = read.DistinguishedName;

            // ⚠️ القراءة دي أحدث ما عندنا عن الحساب، فبنجدّد بيها حالة
            //    التعطيل. من غير كده الشارة في القائمة بتفضل على آخر قيمة
            //    كتبها الاستيراد، فحساب اتعطّل بعده يفضل شكله شغّال لحد
            //    استيراد جاي - والشارة اللي بتتأخّر أسوأ من شارة مش موجودة.
            unit.AdAccountEnabled = read.AccountEnabled;

            // المسار اتغيّر من ورانا؟ نسجّله - نقل الحساب بره النظام معلومة
            // ليها قيمة في المراجعة، ومش المفروض تعدّي بصمت.
            if (!string.Equals(unit.AdDistinguishedName, dn, StringComparison.OrdinalIgnoreCase))
            {
                var oldDn = unit.AdDistinguishedName;
                unit.AdDistinguishedName = dn;
                await _audit.LogAsync("faculty_ad_dn_drift", "FacultyUnits", unit.Id,
                    new List<AuditChangeLog>
                    {
                        new() { FieldName = "distinguishedName", OldValue = oldDn, NewValue = dn }
                    });
            }

            // ⚠️ ‏unit.AdUserPrincipalName بيتحدّث من نفس القراءة: بنقرا الحساب
            //    أصلًا، فقيمة بايتة عندنا وإحنا ماسكين الحيّة في إيدنا مالهاش
            //    مبرّر. والفحص (مقدّمته = اسم الحساب؟) محسوب على الموديل.
            unit.AdUserPrincipalName = NullIfBlank(read.UserPrincipalName);

            var attrs = new Dictionary<string, string>
            {
                ["description"] = current?.FullNameAr ?? "",
                ["employeeID"] = current?.NationalId ?? "",
                ["mobile"] = current?.Mobile ?? "",
                ["company"] = current?.College ?? "",
                ["department"] = current?.Department ?? ""
            };

            // ⚠️ ‏displayName بيتكتب **لو** عندنا اسم إنجليزي مسجَّل. القيمة
            //    الفاضية في الكتابة بتمسح الخاصية من الدليل، والوحدات المسجّلة
            //    قبل اعتماد الخاصية دي مالهاش اسم إنجليزي؛ فالسطر غير المشروط
            //    كان معناه إن أول «إعادة مزامنة» تفضّي displayName عند كل
            //    وحدة منهم - مسح بيانات في الدليل بضغطة زرّ اسمها «مزامنة».
            // ⚠️ والتفضية المقصودة (يشيل الاسم عمدًا) مش مسار موجود أصلًا:
            //    الاسم بيتغيّر مع الساكن، والوحدة الشاغرة بيتقفل صفّها ولا
            //    بيتكتب فيها اسم فاضي.
            if (current != null && !string.IsNullOrWhiteSpace(current.FullNameEn))
                attrs["displayName"] = current.FullNameEn.Trim();

            var res = await _ad.SetUserExtensionAttributesAsync(dn!, attrs);
            if (!res.Success)
                return await FailSync(unit, res.Error ?? "رفض الـAD عملية الكتابة");

            // ============================================================
            //  النقل بين OU البنين والبنات — بعد الكتابة لا قبلها.
            //
            //  ⚠️ الترتيب مقصود: MoveTo بيغيّر الـ DN، فلو نقلنا الأول كان
            //     لازم نقرا الـ DN الجديد من الدومين قبل ما نكتب - نداء زيادة
            //     وفرصة إن الكتابة تفشل على مسار قديم. الكتابة على الـ DN
            //     اللي إحنا متأكدين منه، وبعدها النقل.
            //
            //  ⚠️ وفشل النقل بيخلّي الوحدة Failed حتى لو الخانات اتكتبت:
            //     حساب بالبيانات الجديدة وهو قاعد في قسم غلط أخطر من حساب
            //     ما اتحدّثش - لأنه شكله سليم في كل الشاشات.
            // ============================================================
            string? movedTo = null;
            var move = ResolveOuMove(unit, current?.Gender, dn);
            if (move.Needed)
            {
                var mv = await _ad.MoveUserAsync(dn!, move.TargetOu!);
                if (!mv.Success)
                    return await FailSync(unit,
                        $"تم تحديث البيانات، لكن نقل الحساب إلى {move.TargetOu} فشل: {mv.Error}");

                movedTo = move.TargetOu;
                unit.AdDistinguishedName = mv.NewDistinguishedName ?? unit.AdDistinguishedName;

                await _audit.LogAsync("faculty_ad_ou_move", "FacultyUnits", unit.Id,
                    new List<AuditChangeLog>
                    {
                        new() { FieldName = "distinguishedName", OldValue = dn, NewValue = mv.NewDistinguishedName }
                    });
            }

            unit.SyncState = FacultyUnitSyncState.Synced;
            unit.LastSyncedAt = DateTime.UtcNow;
            unit.LastSyncError = null;
            await _db.SaveChangesAsync();

            // ⚠️ الكتابة في الدليل النشط تُقيَّد بما كُتب حرفيًّا. هذه بيانات شخصية
            //    تخرج من النظام إلى نظام آخر لا زرّ تراجع فيه، وكانت تمرّ بلا أثر:
            //    فلو ظهر في الدليل اسمٌ أو هوية خطأ، لا سبيل لمعرفة أي عملية كتبته.
            await _audit.LogAsync("faculty_ad_push", "FacultyUnits", unit.Id,
                attrs.Select(a => new AuditChangeLog
                {
                    FieldName = a.Key,
                    OldValue = null,
                    NewValue = string.IsNullOrEmpty(a.Value) ? null : a.Value
                }).ToList());
            await _uow.SaveAsync();

            return new AdPushResultDto
            {
                Success = true,
                AttributesWritten = attrs.Count,
                MovedToOu = movedTo,
                SyncState = FacultyUnitSyncState.Synced
            };
        }

        // الأب المباشر لأي DN — للعرض في شاشة الفرق بس.
        // ⚠️ مش بتتستخدم في أي مقارنة ولا في بناء مسار النقل: مسار النقل جاي
        //    من الإعدادات كامل (TargetOuFor)، والمقارنة بتتعمل بـ CheckOuGender.
        //    فلو DN فيه فاصلة متهرّبة (CN=Ali\, Ahmed) أسوأ نتيجة إن السطر
        //    المعروض يطلع مقصوص - مايترتّبش عليه نقل غلط.
        private static string? ParentOu(string? dn)
        {
            if (string.IsNullOrWhiteSpace(dn)) return null;
            var i = dn.IndexOf(',');
            return i > 0 && i < dn.Length - 1 ? dn[(i + 1)..].Trim() : dn;
        }

        private async Task<AdPushResultDto> FailSync(FacultyUnit unit, string error)
        {
            // ⚠️ الفشل بيتسجّل على الوحدة مش بيترمي بس: المستخدم لازم يلاقي
            //    السبب مكتوب لما يرجع للشاشة، مش رسالة عدّت وضاعت.
            unit.SyncState = FacultyUnitSyncState.Failed;
            unit.LastSyncError = error;
            await _db.SaveChangesAsync();
            _logger.LogError("AD push failed for {Account}: {Error}", unit.AdAccount, error);

            // ⚠️ الفشل يُقيَّد أيضًا: محاولةٌ فاشلة على حساب في الدليل معلومة لها
            //    قيمة في المراجعة - لا سيّما إن تكرّرت.
            await _audit.LogAsync("faculty_ad_push_failed", "FacultyUnits", unit.Id,
                new List<AuditChangeLog> { new() { FieldName = "LastSyncError", OldValue = null, NewValue = error } });
            await _uow.SaveAsync();

            return new AdPushResultDto { Success = false, Error = error, SyncState = FacultyUnitSyncState.Failed };
        }

        // =============================================================
        //  تحليل نصّ البحث إلى (برج، شقة، فيلا)
        // =============================================================
        // ⚠️ المستخدم يكتب ما يراه على الشاشة، لا ما هو مخزَّن. والاسم المعروض
        //    مركَّب من عمودين، فلا وجود له في قاعدة البيانات ليُبحث فيه.
        //
        // ⚠️ والكتابة العربية لا تأتي بصيغة واحدة: «شقة» و«شقه»، و«فيلا» و«فِلّة»،
        //    وأرقام هندية ٠١٢ وأخرى عربية 012، ومسافات تُكتب أو تُهمَل، وتطويل
        //    الحروف «بـرج» ينسخه المستخدم أحيانًا مع النصّ. التطبيع أدناه يجعل
        //    هذه كلها مدخلًا واحدًا؛ من دونه يشتكي المستخدم أن «البحث لا يعمل»
        //    وهو يكتب الاسم الصحيح بصيغة أخرى.
        //
        //  «برج 1 - شقة 3» · «برج1 شقه 3» · «برج١شقة٣» · «1-3» → برج ١ شقة ٣
        //  «برج 2»  → كل شقق البرج ٢          «شقة 4» → الشقة ٤ في كل الأبراج
        //  «فيلا 7» · «فله ٧» · «فيله 7»      → الفيلا ٧
        //  ما لا يحوي رقمًا مع كلمة دالّة يُترك للبحث النصّي كما كان.
        private static readonly Regex RxTower = new(@"برج\s*(\d{1,3})", RegexOptions.Compiled);
        private static readonly Regex RxApt   = new(@"(?:شقه|شقق)\s*(\d{1,4})", RegexOptions.Compiled);
        private static readonly Regex RxVilla = new(@"(?:فيلا|فيله|فله|فلل)\s*(\d{1,4})", RegexOptions.Compiled);
        private static readonly Regex RxPair  = new(@"^(\d{1,3})\s*[-/\\_ ]\s*(\d{1,4})$", RegexOptions.Compiled);
        // ⚠️ كلمة النوع وحدها بلا رقم. المستخدم يكتب «فيلا» منتظرًا الفلل كلها،
        //    وكان النصّ يسقط إلى البحث النصّي فيُقارن بحساب الدومين واسم الساكن
        //    والهوية والجوال - ولا شيء منها يحوي كلمة «فيلا»، فيرى جدولًا فارغًا
        //    ويظن أن لا فلل في النظام.
        private static readonly Regex RxTowerWord = new(@"(?:^|\s)(?:برج|ابراج|بروج)(?:\s|$)", RegexOptions.Compiled);
        private static readonly Regex RxAptWord   = new(@"(?:^|\s)(?:شقه|شقق)(?:\s|$)", RegexOptions.Compiled);
        private static readonly Regex RxVillaWord = new(@"(?:^|\s)(?:فيلا|فيله|فله|فلل)(?:\s|$)", RegexOptions.Compiled);
        private static readonly Regex RxSpaces = new(@"\s+", RegexOptions.Compiled);

        public static string NormalizeArabicSearch(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                // الأرقام الهندية والفارسية → أرقام عربية
                if (ch >= '\u0660' && ch <= '\u0669') { sb.Append((char)('0' + (ch - '\u0660'))); continue; }
                if (ch >= '\u06F0' && ch <= '\u06F9') { sb.Append((char)('0' + (ch - '\u06F0'))); continue; }

                switch (ch)
                {
                    case 'ـ': continue;                    // تطويل - يُحذف
                    case 'أ': case 'إ': case 'آ': case 'ٱ': sb.Append('ا'); break;
                    case 'ة': sb.Append('ه'); break;
                    case 'ى': sb.Append('ي'); break;
                    default: sb.Append(ch); break;
                }
            }

            return RxSpaces.Replace(sb.ToString(), " ").Trim();
        }

        public static (int? Tower, int? Apartment, int? Villa, FacultyUnitType? Type) ParseUnitSearch(string? raw)
        {
            var s = NormalizeArabicSearch(raw);
            if (s.Length == 0) return (null, null, null, null);

            int? t = null, a = null, v = null;

            var m = RxTower.Match(s); if (m.Success) t = int.Parse(m.Groups[1].Value);
            m = RxApt.Match(s);       if (m.Success) a = int.Parse(m.Groups[1].Value);
            m = RxVilla.Match(s);     if (m.Success) v = int.Parse(m.Groups[1].Value);

            // «1-3» بلا كلمات: رقمان بينهما فاصل ولا شيء غيرهما. لا نقبل رقمًا
            // مفردًا لأنه يشبه جزءًا من رقم هوية أو جوال، فيُفسد البحث النصّي.
            if (!t.HasValue && !a.HasValue && !v.HasValue)
            {
                m = RxPair.Match(s);
                if (m.Success)
                {
                    t = int.Parse(m.Groups[1].Value);
                    a = int.Parse(m.Groups[2].Value);
                }
            }

            // كلمة النوع وحدها: «فيلا» → الفلل كلها، و«برج» أو «شقة» → الأبراج كلها
            // (الشقق وحدات أبراج). تُقرأ فقط حين لا يوجد رقم، فلا تزاحم البحث الدقيق.
            FacultyUnitType? type = null;
            if (!t.HasValue && !a.HasValue && !v.HasValue)
            {
                if (RxVillaWord.IsMatch(s)) type = FacultyUnitType.Villa;
                else if (RxTowerWord.IsMatch(s) || RxAptWord.IsMatch(s)) type = FacultyUnitType.Tower;
            }

            return (t, a, v, type);
        }

        // ⚠️ رقم معاملة إنجاز أرقام فقط بلا أي حروف أو فواصل. التحقق هنا لأن
        //    الرقم هو الرابط الوحيد بين الطلب في إنجاز وتنفيذه في النظام: أي
        //    اختلاف في صياغته (مسافة، شرطة، بادئة) يجعل البحث عنه لاحقًا يفشل
        //    رغم أن المعاملة مسجّلة فعلًا.
        public static string ValidateTicketNo(string? raw)
        {
            var d = new string((raw ?? "").Where(char.IsDigit).ToArray());
            if (d.Length == 0)
                throw new UserFriendlyException("رقم معاملة إنجاز مطلوب؛ فهو الرابط الوحيد بين الطلب في إنجاز وتنفيذه في النظام", 400);
            if (d.Length != (raw ?? "").Trim().Length)
                throw new UserFriendlyException("رقم معاملة إنجاز يتكوّن من أرقام فقط، دون حروف أو رموز", 400);
            return d;
        }

        // ⚠️ الجوال بيتخزّن ويتكتب في الدومين بصيغة 9665XXXXXXXX. البيانات
        //    القديمة بصيغة 05XXXXXXXX وبتفضل زي ما هي لحد ما الوحدة تتغيّر —
        //    مانعملش تحويل جماعي على بيانات مادخلناهاش ومامتأكدينش منها.
        // ⚠️ التوحيد نفسه في Core/IdentityRules.cs — هنا غلاف يرمي رسالة مفهومة
        //    بدل إرجاع null، لأن هذه الشاشة تدخل الرقم يدويًّا ويجب أن يُصحَّح فورًا.
        public static string? NormalizeMobile(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return IdentityRules.NormalizeMobile(raw)
                ?? throw new UserFriendlyException(IdentityRules.MobileError, 400);
        }

        // ⚠️ القاعدة كانت مكتوبة هنا، وهي أقوى نسخة في النظام — والباقي كان
        //    بيقبل أي عشرة أرقام. اتنقلت لـ Core/IdentityRules عشان الكل ياخدها،
        //    والتفاصيل في تعليقها هناك.
        public static string? ValidateNationalId(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var d = new string(raw.Where(char.IsDigit).ToArray());

            if (IdentityRules.LooksLikeMobile(d))
                throw new UserFriendlyException(IdentityRules.NationalIdIsMobileError, 400);
            if (!IdentityRules.IsValidNationalId(d))
                throw new UserFriendlyException(IdentityRules.NationalIdError, 400);

            return d;
        }

        // ⚠️ الحساب في OU بنين والساكن دكتورة (أو العكس)؟
        //    القراءة من الـ DN المحفوظ — بيتحدّث مع كل مزامنة فبيعكس الواقع.
        //    الفلل مالهاش توقّع لأن الـ OU بتاعتها مش مقسّمة أصلًا.
        //    ⚠️ الدالة دي بتكتشف التعارض وبس - النقل نفسه في ResolveOuMove
        //    وبيتنفّذ مع الكتابة في الدومين (PushToAdAsync). التعليق هنا كان
        //    لسه بيقول «النظام مابينقلش الحساب» بعد ما النقل اتنفّذ فعلًا،
        //    وتعليق بيكدب أسوأ من تعليق مش موجود.
        // ============================================================================
        //  نقل الحساب بين OU البنين وOU البنات لمّا جنس الشاغل يتغيّر.
        //
        //  ⚠️ الحالة اللي بتحصل فعلًا: bu6ap20 كان ساكنه عضو هيئة تدريس فحسابه
        //     في OU=MALE. جت معاملة بعضوة هيئة تدريس، فاتغيّرت بيانات الحساب
        //     (الاسم والهوية والجوال) بس الحساب فضل مكانه في OU البنين. تقسيم
        //     الـ OU في الدومين مش تنظيم على ورق - عليه صلاحيات وسياسات
        //     مجموعة، فحساب في القسم الغلط معناه شاغل بصلاحيات مش بتاعته.
        //
        //  ⚠️ والقرار مبني على CheckOuGender نفسها اللي بتحسب «التعارض» في
        //     الجدول ولوحة التحكم - مش على مقارنة تانية. لو اتفارقوا، الشاشة
        //     هتقول «فيه تعارض» والنقل يقول «مافيش» أو العكس.
        //
        //  ⚠️ بنرجّع null لو التقسيم مش معمول لنوع الوحدة (الفلل ممكن تبقى OU
        //     واحدة مختلطة) أو لو ناحية من الاتنين مش مضبوطة في الإعدادات.
        //     نقل لمسار فاضي بيودّي الحساب لجذر الدومين - أسوأ بكتير من إنه
        //     يفضل مكانه ويتصلّح بالإيد.
        // ============================================================================
        private (bool Needed, string? TargetOu, string? Note) ResolveOuMove(
            FacultyUnit unit, Gender? gender, string? dn)
        {
            var check = CheckOuGender(dn, gender);
            if (!check.Mismatch) return (false, null, null);

            var target = _config.TargetOuFor(unit.UnitType, gender);
            if (string.IsNullOrWhiteSpace(target))
                return (false, null, "الحساب في القسم الغلط، لكن مسار القسم الصحيح غير مضبوط في الإعدادات - النقل يحتاج تدخّلًا يدويًّا");

            return (true, target, check.Note);
        }

        private static (bool Mismatch, string? Note) CheckOuGender(string? dn, Gender? occupantGender)
        {
            if (occupantGender == null || string.IsNullOrWhiteSpace(dn)) return (false, null);

            var inMale = dn.Contains("OU=MALE,", StringComparison.OrdinalIgnoreCase);
            var inFemale = dn.Contains("OU=FEMALE,", StringComparison.OrdinalIgnoreCase);
            if (!inMale && !inFemale) return (false, null);

            // ⚠️ «قسم عضوات / أعضاء هيئة التدريس» لا «وحدة الطالبات / الطلاب»:
            //    نفس الغلط اللي كان في أسماء الوحدات التنظيمية فوق - النصّ ده
            //    بيتعرض في tooltip الشارة على صف الجدول.
            if (inFemale && occupantGender == Gender.Male)
                return (true, "الحساب في قسم عضوات هيئة التدريس وشاغله عضو هيئة تدريس");
            if (inMale && occupantGender == Gender.Female)
                return (true, "الحساب في قسم أعضاء هيئة التدريس وشاغلته عضوة هيئة تدريس");

            return (false, null);
        }

        private static string? NullIfBlank(string? v)
            => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        // ⚠️ الاستيراد ممكن يتنفّذ من مهمة مجدولة مالهاش مستخدم. GetCurrentUserId
        //    بيرمي في الحالة دي، فبنرجّع null بدل ما العملية كلها تقع.
        private int? SafeUserId()
        {
            try
            {
                var id = _uow.GetCurrentUserId();
                return id > 0 ? id : null;
            }
            catch { return null; }
        }
    }
}
