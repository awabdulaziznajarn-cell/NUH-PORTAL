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
        // ⚠️ الخصائص الخمسة دي هي كل اللي النظام بيكتبه في الدومين — لا أكتر.
        //    displayName و givenName و sn سايبينهم عن قصد: في وحدات السكن
        //    القيمة بتاعتهم هي اسم الوحدة نفسها (villa08)، مش اسم الساكن.
        //    لو كتبنا فيهم اسم الدكتور نبقى غيّرنا عرف قايم في الدومين من غير
        //    سبب، وكسرنا أي حاجة بتعرف الوحدة من اسمها.
        public static readonly string[] ManagedAttributes =
            { "description", "employeeID", "mobile", "company", "department" };

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

            foreach (var (label, ou) in labelled)
            {
                var res = await _ad.CheckOuAccessAsync(ou, ManagedAttributes);
                dto.Ous.Add(new OuAccessDto
                {
                    Label = label,
                    OrganizationalUnit = ou,
                    OuExists = res.OuExists,
                    CanRead = res.CanRead,
                    AllWritable = res.AllWritable,
                    ProbedAccount = res.ProbedAccount,
                    AttributeWritable = new Dictionary<string, bool>(res.AttributeWritable),
                    Error = res.Error
                });
            }

            dto.AllOk = dto.Ous.Count > 0 && dto.Ous.All(o => o.CanRead && o.AllWritable);
            return dto;
        }

        private List<(string Label, string Ou)> LabelledOus()
        {
            var list = new List<(string, string)>();
            if (!string.IsNullOrWhiteSpace(_config.TowersMaleOu)) list.Add(("الأبراج - قسم الطلاب", _config.TowersMaleOu));
            if (!string.IsNullOrWhiteSpace(_config.TowersFemaleOu)) list.Add(("الأبراج - قسم الطالبات", _config.TowersFemaleOu));
            if (!string.IsNullOrWhiteSpace(_config.VillasOu)) list.Add(("الفلل", _config.VillasOu));
            if (!string.IsNullOrWhiteSpace(_config.VillasMaleOu)) list.Add(("الفلل - قسم الطلاب", _config.VillasMaleOu!));
            if (!string.IsNullOrWhiteSpace(_config.VillasFemaleOu)) list.Add(("الفلل - قسم الطالبات", _config.VillasFemaleOu!));
            return list;
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

            foreach (var (label, ou) in labelled)
            {
                var res = await _ad.SearchOuUsersAsync(ou, _config.MaxImportResults);
                if (!res.Success)
                {
                    dto.Errors.Add($"{label}: {res.Error}");
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
                            OrganizationalUnit = label,
                            Action = ImportRowAction.Ignored,
                            ActionLabel = "مُستبعَد",
                            DisplayName = u.SamAccountName,
                            Description = u.Description,
                            AccountEnabled = u.AccountEnabled,
                            IgnoreReason = "الاسم لا يطابق صيغة وحدات السكن (برج أو فيلا)"
                        });
                        dto.IgnoredCount++;
                        continue;
                    }

                    existing.TryGetValue(u.SamAccountName, out var known);
                    var action = known == null
                        ? ImportRowAction.New
                        : (known.AdDistinguishedName != u.DistinguishedName
                           || known.AdAccountEnabled != u.AccountEnabled
                           || known.NameMatchesStandard != parsed.MatchesStandard)
                            ? ImportRowAction.Changed
                            : ImportRowAction.Unchanged;

                    dto.Rows.Add(new ImportRowDto
                    {
                        AdAccount = u.SamAccountName,
                        DistinguishedName = u.DistinguishedName,
                        OrganizationalUnit = label,
                        Action = action,
                        ActionLabel = action switch
                        {
                            ImportRowAction.New => "جديدة - ستُسجَّل",
                            ImportRowAction.Changed => "مسجّلة - سيتم تحديثها",
                            _ => "مسجّلة - دون تغيير"
                        },
                        UnitType = parsed.UnitType,
                        TowerNo = parsed.TowerNo,
                        ApartmentNo = parsed.ApartmentNo,
                        VillaNo = parsed.VillaNo,
                        DisplayName = parsed.UnitType == FacultyUnitType.Villa
                            ? $"فيلا {parsed.VillaNo}"
                            : $"برج {parsed.TowerNo} - شقة {parsed.ApartmentNo}",
                        NameMatchesStandard = parsed.MatchesStandard,
                        Deviation = parsed.Deviation,
                        Description = u.Description,
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
                    unit.AdAccountEnabled = row.AccountEnabled;
                    unit.NameMatchesStandard = row.NameMatchesStandard;
                    unit.SyncState = FacultyUnitSyncState.Synced;
                    unit.LastSyncedAt = now;
                    unit.LastSyncError = null;

                    // ⚠️ صف الإشغال بيتفتح مرة واحدة بس: لو الوحدة عندها ساكن
                    //    مفتوح خلاص مانلمسهوش. إعادة تشغيل الاستيراد مالهاش
                    //    حق تقفل ساكن ولا تفتح صف جديد — دي إجراءات بقرار من
                    //    شاشة التسليم، والاستيراد مجرد قراءة.
                    var hasOpen = unit.Occupancies.Any(o => o.EndDate == null);
                    if (!hasOpen && !string.IsNullOrWhiteSpace(row.Description))
                    {
                        _db.FacultyOccupancies.Add(new FacultyOccupancy
                        {
                            Unit = unit,
                            FullNameAr = row.Description!.Trim(),
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
            bool onlyDeviations = false, bool onlyNeedsConfirm = false,
            int? tower = null, int page = 1, int pageSize = 50)
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
                var (pt, pa, pv) = ParseUnitSearch(s);

                if (pt.HasValue || pa.HasValue || pv.HasValue)
                {
                    q = q.Where(x =>
                        (pv.HasValue && x.Unit.UnitType == FacultyUnitType.Villa && x.Unit.VillaNo == pv)
                        || ((pt.HasValue || pa.HasValue)
                            && x.Unit.UnitType == FacultyUnitType.Tower
                            && (!pt.HasValue || x.Unit.TowerNo == pt)
                            && (!pa.HasValue || x.Unit.ApartmentNo == pa))
                        || x.Unit.AdAccount.Contains(s)
                        || (x.Current != null && x.Current.FullNameAr.Contains(s)));
                }
                else
                {
                    q = q.Where(x => x.Unit.AdAccount.Contains(s)
                                     || (x.Current != null && x.Current.FullNameAr.Contains(s))
                                     || (x.Current != null && x.Current.NationalId != null && x.Current.NationalId.Contains(s))
                                     || (x.Current != null && x.Current.Mobile != null && x.Current.Mobile.Contains(s)));
                }
            }

            var total = await q.CountAsync();

            var rows = await q
                .OrderBy(x => x.Unit.UnitType)
                .ThenBy(x => x.Unit.TowerNo ?? 0)
                .ThenBy(x => x.Unit.ApartmentNo ?? x.Unit.VillaNo ?? 0)
                .ThenBy(x => x.Unit.AdAccount)
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
                    OuGenderMismatchNote = CheckOuGender(x.Unit.AdDistinguishedName, x.Current?.Gender).Note
                }).ToList()
            };

            // أرقام الأبراج الموجودة فعلًا — بتملا قائمة الفلتر بدل ما نفترض ١..١٣
            dto.Towers = await _db.FacultyUnits.AsNoTracking()
                .Where(u => u.UnitType == FacultyUnitType.Tower && u.TowerNo != null)
                .Select(u => u.TowerNo!.Value).Distinct().OrderBy(n => n).ToListAsync();

            await FillCountersAsync(dto, confirmCutoff);
            return dto;
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
                    OuGenderMismatchNote = CheckOuGender(unit.AdDistinguishedName, current?.Gender).Note
                },
                History = history.Select(o => new OccupancyHistoryItemDto
                {
                    Id = o.Id,
                    FullNameAr = o.FullNameAr,
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

            // ⚠️ بتتعرض للتوضيح بس ومابتتكتبش: في وحدات السكن القيمة بتاعتها هي
            //    اسم الوحدة نفسها (villa08) مش اسم الساكن. لو كتبنا فيها اسم
            //    الدكتور نبقى كسرنا عرف قايم في الدومين من غير سبب.
            dto.Lines.Add(new AdDiffLineDto
            {
                Attribute = "displayName", CurrentValue = read.DisplayName, NewValue = read.DisplayName,
                WillChange = false, Note = "لا يتغيّر - اسم الوحدة"
            });

            return dto;
        }

        public async Task<AdPushResultDto> PushToAdAsync(int unitId)
        {
            var unit = await _db.FacultyUnits
                .Include(u => u.Occupancies)
                .FirstOrDefaultAsync(u => u.Id == unitId)
                ?? throw new UserFriendlyException("الوحدة غير موجودة", 404);

            var current = unit.Occupancies.FirstOrDefault(o => o.EndDate == null);

            var dn = unit.AdDistinguishedName;
            if (string.IsNullOrWhiteSpace(dn))
            {
                // ⚠️ الـ DN ممكن يكون اتغيّر لو الحساب اتنقل لـ OU تانية. بنقراه
                //    من الدومين بدل ما نعتمد على المحفوظ ونفشل بـ object not found.
                var read = await _ad.GetUserBySamAccountNameAsync(unit.AdAccount);
                if (!read.Success)
                    return await FailSync(unit, read.Error ?? "الحساب غير موجود في الدومين");
                dn = read.DistinguishedName;
                unit.AdDistinguishedName = dn;
            }

            var attrs = new Dictionary<string, string>
            {
                ["description"] = current?.FullNameAr ?? "",
                ["employeeID"] = current?.NationalId ?? "",
                ["mobile"] = current?.Mobile ?? "",
                ["company"] = current?.College ?? "",
                ["department"] = current?.Department ?? ""
            };

            var res = await _ad.SetUserExtensionAttributesAsync(dn!, attrs);
            if (!res.Success)
                return await FailSync(unit, res.Error ?? "رفض الدومين عملية الكتابة");

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

            return new AdPushResultDto { Success = true, AttributesWritten = attrs.Count, SyncState = FacultyUnitSyncState.Synced };
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

        public static (int? Tower, int? Apartment, int? Villa) ParseUnitSearch(string? raw)
        {
            var s = NormalizeArabicSearch(raw);
            if (s.Length == 0) return (null, null, null);

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

            return (t, a, v);
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

        // ⚠️ الهوية السعودية بتبدأ بـ 1 والإقامة بـ 2، والجوال بيبدأ بـ 05.
        //    مفيش تداخل بينهم، فالفحص ده مابيرفضش رقم صحيح أبدًا.
        //    الفحص موجود لأن الدومين فيه 135 حساب مكتوب في employeeID بتاعهم
        //    رقم الجوال بدل الهوية - بيانات موروثة. الفحص بيمنع تكرارها.
        public static string? ValidateNationalId(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var d = new string(raw.Where(char.IsDigit).ToArray());

            if (d.StartsWith("05") || d.StartsWith("9665"))
                throw new UserFriendlyException("القيمة المُدخلة رقم جوال وليست رقم هوية. رقم الهوية يبدأ بالرقم 1، والإقامة بالرقم 2.", 400);
            if (d.Length != 10 || (d[0] != '1' && d[0] != '2'))
                throw new UserFriendlyException("رقم الهوية يجب أن يتكوّن من 10 أرقام وأن يبدأ بالرقم 1 (هوية وطنية) أو 2 (إقامة)", 400);

            return d;
        }

        // ⚠️ الحساب في OU بنين والساكن دكتورة (أو العكس)؟
        //    القراءة من الـ DN المحفوظ — بيتحدّث مع كل مزامنة فبيعكس الواقع.
        //    الفلل مالهاش توقّع لأن الـ OU بتاعتها مش مقسّمة أصلًا.
        //    ⚠️ ده عدّاد وبس: النظام مابينقلش الحساب. النقل محتاج صلاحية حذف
        //    وإنشاء كائنات، والقرار مؤجّل لحد ما الرقم نفسه يقوله.
        private static (bool Mismatch, string? Note) CheckOuGender(string? dn, Gender? occupantGender)
        {
            if (occupantGender == null || string.IsNullOrWhiteSpace(dn)) return (false, null);

            var inMale = dn.Contains("OU=MALE,", StringComparison.OrdinalIgnoreCase);
            var inFemale = dn.Contains("OU=FEMALE,", StringComparison.OrdinalIgnoreCase);
            if (!inMale && !inFemale) return (false, null);

            if (inFemale && occupantGender == Gender.Male)
                return (true, "الحساب ضمن وحدة الطالبات وشاغله عضو هيئة تدريس");
            if (inMale && occupantGender == Gender.Female)
                return (true, "الحساب ضمن وحدة الطلاب وشاغلته عضوة هيئة تدريس");

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
