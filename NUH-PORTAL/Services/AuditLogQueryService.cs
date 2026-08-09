using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.AuditLogs;
using NUH_PORTAL.DTOs.Common;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // استعلامات وتقارير سجل العمليات — اتنقل من AuditLogsController
    // تحسينات: 4 استعلامات إحصائيات بقوا 2 (GroupBy)، وشيلنا الـ double-serialization بتاع تقدير الحجم
    public class AuditLogQueryService : AppServiceBase, IAuditLogQueryService
    {
        private readonly IRepository<AuditLog> _logs;
        private readonly IRepository<User> _users;

        private static readonly string[] StudentActions = { "create_student", "update_student", "delete_student", "checkout_student" };
        private static readonly string[] RequestActions =
        {
            "create_request", "approve_request", "reject_request",
            "housing_approve_request", "housing_reject_request",
            "submit_cyber_review",
            "cyber_approve_request", "cyber_reject_request"
        };
        private static readonly string[] LoginActions = { "login", "login_failed", "logout", "login_admin_fallback", "login_admin_fallback_failed" };

        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly IMemoryCache _cache;
        private List<int>? _hiddenActorsCache;

        public AuditLogQueryService(
            IRepository<AuditLog> logs,
            IRepository<User> users,
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            IMemoryCache cache,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _logs = logs;
            _users = users;
            _userManager = userManager;
            _roleManager = roleManager;
            _cache = cache;
        }

        // ====================================================================
        //  نطاق الرؤية في سجل العمليات — قاعدة واحدة يمرّ منها كل استعلام.
        //
        //  ⚠️ كان السجل مفتوحًا بالكامل لكل من يملك auditLogs.view: مشرف الإسكان
        //     يرى من الأمن السيبراني اعتمد ومن رفض، والعكس. وهذه ليست تفصيلة
        //     عرض — إجراءات كل إدارة تخصّها، ومن يراها كلها يجب أن يُمنح ذلك صراحة.
        //
        //  القاعدة:
        //     • مع صلاحية auditLogs.viewAll  → السجل كامل بلا استثناء.
        //     • بدونها → يُخفى ما نفّذه **موظفو الإدارات الأخرى** فقط.
        //       ويبقى ظاهرًا: إجراءات إدارتك، وإجراءات الطلاب، وإجراءاتك أنت،
        //       وإجراءات النظام التي بلا منفّذ (إرسال رمز التحقق مثلًا).
        //
        //  ⚠️ الإخفاء بقائمة موظفي الإدارات الأخرى لا بقائمة المسموح بهم: عدد
        //     الموظفين محدود، أما الطلاب فبالآلاف — فقائمة «المسموح» كانت ستصير
        //     استعلامًا ضخمًا، وأي حساب جديد يسقط منها بصمت.
        // ====================================================================
        // ⚠️ الحساب يمرّ على كل الأدوار ويسأل عن أعضاء كل دور — أي أربعة استعلامات
        //    إضافية على كل فتحة للشاشة، وقد ظهر ذلك كتأخير محسوس. عضوية الأدوار
        //    نادرة التغيّر (خمسة موظفين)، فدقيقة تخزين تُلغي التكلفة عمليًا،
        //    وأقصى تأخير لسريان تغيير في الأدوار دقيقة واحدة — وهو نفس ما تفعله
        //    PermissionClaimsTransformation بالضبط، فالسلوك متسق لا مفاجئ.
        private const string ScopeCachePrefix = "auditscope::";
        private static readonly TimeSpan ScopeCacheFor = TimeSpan.FromSeconds(60);

        private async Task<List<int>> HiddenActorIdsAsync()
        {
            if (_hiddenActorsCache != null) return _hiddenActorsCache;

            var myRole = UnitOfWork.GetCurrentUserRole() ?? "";
            var cacheKey = ScopeCachePrefix + myRole.ToLowerInvariant();
            if (_cache.TryGetValue(cacheKey, out List<int>? cached) && cached != null)
            {
                _hiddenActorsCache = cached;
                return cached;
            }

            var hidden = new List<int>();

            var allRoles = _roleManager.Roles.Select(r => r.Name).ToList();

            foreach (var role in allRoles)
            {
                if (string.IsNullOrWhiteSpace(role)) continue;
                if (string.Equals(role, myRole, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)) continue;   // الطلاب

                var users = await _userManager.GetUsersInRoleAsync(role);
                hidden.AddRange(users.Select(u => u.Id));
            }

            _hiddenActorsCache = hidden.Distinct().ToList();
            _cache.Set(cacheKey, _hiddenActorsCache, ScopeCacheFor);
            return _hiddenActorsCache;
        }

        // كل استعلام في هذا الملف يبدأ من هنا — لا من _logs.Query() مباشرة.
        //
        // ⚠️ لماذا شرط مبنيّ يدويًا بدل List.Contains:
        //    كان الشرط `!hidden.Contains(a.user_id.Value)` وهو أنظف في القراءة،
        //    لكن EF Core يترجم قائمة مُمرَّرة كوسيط إلى نص JSON في وسيط واحد
        //    من نوع nvarchar(4000) يفكّه بـ OPENJSON. و OPENJSON بلا إحصائيات،
        //    فيقدّر المحرّك عدد صفوفه تقديرًا ثابتًا بعيدًا عن الواقع، ويطلب
        //    على أساسه منحة ذاكرة ضخمة لا يحتاجها.
        //
        //    وعلى SQL Server Express — وهو المستخدم هنا — سقف الذاكرة ١.٤ جيجا
        //    مهما كانت ذاكرة الجهاز، وحوض منح الذاكرة جزء صغير منها. فطلب واحد
        //    مبالغ فيه يحجز الحوض، وكل استعلام آخر في السيرفر ينتظر عليه
        //    (RESOURCE_SEMAPHORE) — حتى استعلامات SSMS نفسها. وهذا ما ظهر فعلًا:
        //    نداءات ترجع في ٣ مللي وأخرى في ٢٥ و٥٠ ثانية، وكلها تنتهي بنجاح،
        //    ولا جلسة تقفل على أخرى.
        //
        //    العدد هنا صغير ومعروف (موظفو الإدارات الأخرى — خمسة تقريبًا)، فبناء
        //    الشرط بقيم ثابتة يعطي خطة مستقرة بمنحة ذاكرة تافهة. ولو كبر العدد
        //    يومًا فالمقارنة تظل رخيصة لأن user_id مفهرس.
        private async Task<IQueryable<AuditLog>> ScopedAsync()
        {
            var q = _logs.Query().AsNoTracking();
            if (UnitOfWork.HasPermission(ApplicationPermissions.ViewAllAuditLogs.Value))
                return q;

            var hidden = await HiddenActorIdsAsync();
            if (hidden.Count == 0) return q;

            var me = UnitOfWork.GetCurrentUserId();

            // مرئي = بلا منفّذ (إجراء نظام) أو أنا أو ليس من المخفيين
            var visible = PredicateOr(hidden, me);
            return q.Where(visible);
        }

        // يبني: a => a.user_id == null || a.user_id == me
        //            || (a.user_id != h1 && a.user_id != h2 && ...)
        // بقيم ثابتة داخل الشجرة، فلا وسيط قائمة ولا OPENJSON.
        private static System.Linq.Expressions.Expression<Func<AuditLog, bool>> PredicateOr(
            List<int> hidden, int me)
        {
            var a = System.Linq.Expressions.Expression.Parameter(typeof(AuditLog), "a");
            var userId = System.Linq.Expressions.Expression.Property(a, nameof(AuditLog.user_id));

            var isNull = System.Linq.Expressions.Expression.Equal(
                userId, System.Linq.Expressions.Expression.Constant(null, typeof(int?)));

            var isMe = System.Linq.Expressions.Expression.Equal(
                userId, System.Linq.Expressions.Expression.Constant(me, typeof(int?)));

            System.Linq.Expressions.Expression notHidden =
                System.Linq.Expressions.Expression.Constant(true);

            foreach (var id in hidden)
                notHidden = System.Linq.Expressions.Expression.AndAlso(
                    notHidden,
                    System.Linq.Expressions.Expression.NotEqual(
                        userId, System.Linq.Expressions.Expression.Constant(id, typeof(int?))));

            var body = System.Linq.Expressions.Expression.OrElse(
                System.Linq.Expressions.Expression.OrElse(isNull, isMe), notHidden);

            return System.Linq.Expressions.Expression.Lambda<Func<AuditLog, bool>>(body, a);
        }

        public async Task<AuditLogsPageDto> GetLogsAsync(int page, int pageSize, AuditLogFilter filter)
        {
            var filteredQuery = ApplyFilters((await ScopedAsync()), filter);

            // استعلام واحد بيجمع العدادات بدل 3 استعلامات COUNT منفصلة
            var actionCounts = await filteredQuery
                .GroupBy(a => a.action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalRecords = actionCounts.Sum(x => x.Count);
            var studentOps = actionCounts.Where(x => StudentActions.Contains(x.Action)).Sum(x => x.Count);
            var requestOps = actionCounts.Where(x => RequestActions.Contains(x.Action)).Sum(x => x.Count);

            var totalUsers = await filteredQuery.Select(a => a.user_id).Distinct().CountAsync();

            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            // الترتيب حسب العمود المختار (الافتراضي: التاريخ تنازليًا — الأحدث أولًا)
            var sortBy = (filter.SortBy ?? "").ToLowerInvariant();
            var orderedQuery = (sortBy, filter.SortAsc) switch
            {
                ("user", true) => filteredQuery.OrderBy(a => a.User!.full_name),
                ("user", false) => filteredQuery.OrderByDescending(a => a.User!.full_name),
                ("action", true) => filteredQuery.OrderBy(a => a.action),
                ("action", false) => filteredQuery.OrderByDescending(a => a.action),
                ("table", true) => filteredQuery.OrderBy(a => a.target_table),
                ("table", false) => filteredQuery.OrderByDescending(a => a.target_table),
                ("targetid", true) => filteredQuery.OrderBy(a => a.target_id),
                ("targetid", false) => filteredQuery.OrderByDescending(a => a.target_id),
                ("date", true) => filteredQuery.OrderBy(a => a.action_at),
                _ => filteredQuery.OrderByDescending(a => a.action_at)
            };

            var logs = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogItemDto
                {
                    Id = a.Id,
                    user_id = a.user_id,
                    action = a.action,
                    target_table = a.target_table,
                    target_id = a.target_id,
                    action_at = a.action_at,
                    ip_address = a.ip_address,
                    user_agent = a.user_agent,
                    user_name = a.User != null ? (a.User.full_name ?? a.User.UserName) : null,
                    user = a.User != null
                        ? new AuditLogUserDto { full_name = a.User.full_name, username = a.User.UserName }
                        : null,
                    changes = a.AuditChangeLogs != null
                        ? a.AuditChangeLogs.Select(c => new AuditChangeDto { FieldName = c.FieldName, OldValue = c.OldValue, NewValue = c.NewValue }).ToList()
                        : null
                })
                // ⚠️ تحميل مجموعة التغييرات مع كل صف في استعلام واحد ينتج ضربًا
                //    ديكارتيًا يحذّر منه EF صراحةً في السجل. الضرب ده يكبّر حجم
                //    النتيجة الوسيطة، ومعاه منحة الذاكرة المطلوبة — على Express
                //    ده بالظبط اللي بيخنق حوض المنح. استعلامان صغيران أرخص.
                .AsSplitQuery()
                .ToListAsync();

            return new AuditLogsPageDto
            {
                Data = logs,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                Stats = new AuditLogsStatsDto
                {
                    TotalRecords = totalRecords,
                    TotalUsers = totalUsers,
                    StudentOperations = studentOps,
                    RequestOperations = requestOps
                }
            };
        }

        public async Task<FileResultDto> ExportLogsAsync(AuditLogFilter filter)
        {
            var filteredQuery = ApplyFilters((await ScopedAsync()), filter);

            var logs = await filteredQuery
                .OrderByDescending(a => a.action_at)
                .Select(a => new
                {
                    a.Id,
                    user_name = a.User != null ? (a.User.full_name ?? a.User.UserName) : null,
                    a.action,
                    a.target_table,
                    a.target_id,
                    a.action_at,
                    a.ip_address,
                    a.user_agent
                })
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("AuditLogs");

            var headers = new[] { "#", "User", "Action", "Table", "Target ID", "Date", "IP Address", "User Agent" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.Navy;
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                ws.Cell(1, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int i = 0; i < logs.Count; i++)
            {
                var l = logs[i];
                ws.Cell(i + 2, 1).Value = i + 1;
                ws.Cell(i + 2, 2).Value = l.user_name ?? "";
                ws.Cell(i + 2, 3).Value = l.action ?? "";
                ws.Cell(i + 2, 4).Value = l.target_table ?? "";
                ws.Cell(i + 2, 5).Value = l.target_id;
                ws.Cell(i + 2, 6).Value = l.action_at.ToString("yyyy-MM-dd HH:mm:ss");
                ws.Cell(i + 2, 7).Value = l.ip_address ?? "";
                ws.Cell(i + 2, 8).Value = l.user_agent ?? "";
            }

            ws.RangeUsed()?.SetAutoFilter();
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            return new FileResultDto
            {
                Content = stream.ToArray(),
                FileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        public async Task<ChartDataDto> GetChartDataAsync()
        {
            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7).Date;
            var thirtyDaysAgo = now.AddDays(-30).Date;

            var last7Days = await (await ScopedAsync())
                .Where(a => a.action_at >= sevenDaysAgo)
                .GroupBy(a => a.action_at.Date)
                .Select(g => new DateCountDto { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var login30 = await (await ScopedAsync())
                .Where(a => a.action_at >= thirtyDaysAgo &&
                    (a.action == "login" || a.action == "login_failed" || a.action == "logout"))
                .GroupBy(a => a.action_at.Date)
                .Select(g => new DateCountDto { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var studentOps = await (await ScopedAsync())
                .Where(a => a.action_at >= sevenDaysAgo &&
                    (a.action == "create_student" || a.action == "update_student" || a.action == "delete_student"))
                .GroupBy(a => a.action)
                .Select(g => new ActionCountDto { Action = g.Key, Count = g.Count() })
                .ToListAsync();

            var requestOps = await (await ScopedAsync())
                .Where(a => a.action_at >= sevenDaysAgo && RequestActions.Contains(a.action!))
                .GroupBy(a => a.action)
                .Select(g => new ActionCountDto { Action = g.Key, Count = g.Count() })
                .ToListAsync();

            return new ChartDataDto
            {
                Last7Days = last7Days,
                Login30 = login30,
                StudentOps = studentOps,
                RequestOps = requestOps
            };
        }

        public async Task<List<AlertDto>> GetAlertsAsync()
        {
            var now = DateTime.UtcNow;
            var alerts = new List<AlertDto>();

            var tenMinAgo = now.AddMinutes(-10);
            var thirtyMinAgo = now.AddMinutes(-30);

            var failedLogins = await (await ScopedAsync())
                .Where(a => a.action == "login_failed" && a.action_at >= tenMinAgo)
                .CountAsync();
            if (failedLogins >= 5)
                alerts.Add(new AlertDto { Type = "failed_login_explosion", Severity = "high", Count = failedLogins, message_ar = "نشاط تسجيل دخول مشبوه", message_en = "Suspicious Login Activity" });

            var deletes = await (await ScopedAsync())
                .Where(a => a.action == "delete_student" && a.action_at >= tenMinAgo)
                .CountAsync();
            if (deletes >= 5)
                alerts.Add(new AlertDto { Type = "excessive_deletes", Severity = "high", Count = deletes, message_ar = "حذف متكرر للطلاب", message_en = "Excessive Student Deletions" });

            var rejections = await (await ScopedAsync())
                .Where(a => (a.action == "reject_request" || a.action == "housing_reject_request" || a.action == "cyber_reject_request") && a.action_at >= thirtyMinAgo)
                .CountAsync();
            if (rejections >= 10)
                alerts.Add(new AlertDto { Type = "excessive_rejections", Severity = "medium", Count = rejections, message_ar = "عدد مرتفع من الطلبات المرفوضة", message_en = "High Request Rejection Volume" });

            return alerts;
        }

        public async Task<string> GetReportHtmlAsync(string? type, int? userId, string? fromDate, string? toDate, string lang)
        {
            var filteredQuery = ApplyFilters((await ScopedAsync()),
                new AuditLogFilter { UserId = userId, FromDate = fromDate, ToDate = toDate });

            if (type == "login")
                filteredQuery = filteredQuery.Where(a => LoginActions.Contains(a.action!));
            else if (type == "student")
                filteredQuery = filteredQuery.Where(a =>
                    a.action == "create_student" || a.action == "update_student" || a.action == "delete_student");
            else if (type == "request")
                filteredQuery = filteredQuery.Where(a => RequestActions.Contains(a.action!));

            var logs = await filteredQuery
                .OrderByDescending(a => a.action_at)
                .Take(500)
                .Select(a => new
                {
                    user_name = a.User != null ? (a.User.full_name ?? a.User.UserName) : null,
                    a.action,
                    a.target_table,
                    a.target_id,
                    a.action_at,
                    a.ip_address
                })
                .ToListAsync();

            var title = type switch
            {
                "login" => lang == "ar" ? "تقرير نشاط تسجيل الدخول" : "Login Activity Report",
                "student" => lang == "ar" ? "تقرير عمليات الطلاب" : "Student Operations Report",
                "request" => lang == "ar" ? "تقرير عمليات الطلبات" : "Request Operations Report",
                _ => lang == "ar" ? "تقرير سجل العمليات" : "Audit Activity Report"
            };

            var html = $@"<!DOCTYPE html>
<html lang='{lang}' dir='{(lang == "ar" ? "rtl" : "ltr")}'>
<head><meta charset='UTF-8'><title>{title}</title>
<style>
body{{font-family:'Segoe UI',Tahoma,sans-serif;margin:40px;color:#1B2A5E}}
h1{{color:#1B2A5E;border-bottom:3px solid #C9A84C;padding-bottom:10px}}
.header{{display:flex;justify-content:space-between;align-items:center;margin-bottom:30px}}
.logo{{font-size:24px;font-weight:800;color:#1B2A5E}}
table{{width:100%;border-collapse:collapse;margin-top:20px}}
th{{background:#1B2A5E;color:#fff;padding:10px 12px;text-align:{(lang == "ar" ? "right" : "left")};font-size:13px}}
td{{padding:8px 12px;border-bottom:1px solid #DDE3F0;font-size:12px}}
tr:nth-child(even){{background:#F4F6FB}}
.footer{{margin-top:30px;font-size:11px;color:#8891A8;text-align:center;border-top:1px solid #DDE3F0;padding-top:15px}}
.print-btn{{background:#1B2A5E;color:#fff;border:none;padding:10px 24px;border-radius:6px;cursor:pointer;font-size:14px;margin-bottom:20px}}
@media print{{.print-btn{{display:none}}}}
</style></head>
<body>
<div class='header'><div class='logo'>🏠 NUH Portal</div><div>{DateTime.Now:yyyy-MM-dd HH:mm}</div></div>
<h1>{title}</h1>
<p style='color:#8891A8;margin-bottom:20px'>{(lang == "ar" ? "إجمالي السجلات" : "Total Records")}: {logs.Count}</p>
<button class='print-btn' onclick='window.print()'>{(lang == "ar" ? "طباعة / PDF" : "Print / PDF")}</button>
<table><thead><tr>
<th>#</th><th>{(lang == "ar" ? "المستخدم" : "User")}</th><th>{(lang == "ar" ? "الإجراء" : "Action")}</th><th>{(lang == "ar" ? "الجدول" : "Table")}</th><th>{(lang == "ar" ? "التاريخ" : "Date")}</th><th>IP</th>
</tr></thead><tbody>";
            int idx = 1;
            foreach (var l in logs)
            {
                html += $"<tr><td>{idx++}</td><td>{System.Net.WebUtility.HtmlEncode(l.user_name ?? "")}</td><td>{System.Net.WebUtility.HtmlEncode(l.action ?? "")}</td><td>{System.Net.WebUtility.HtmlEncode(l.target_table ?? "")}</td><td>{l.action_at:yyyy-MM-dd HH:mm}</td><td>{System.Net.WebUtility.HtmlEncode(l.ip_address ?? "")}</td></tr>";
            }
            // الفوتر كان verbatim من غير $ في الكود القديم فكان بيطبع {DateTime.Now} حرفيًا — اتصلح
            html += $@"</tbody></table>
<div class='footer'>NUH Housing Portal - {DateTime.Now:yyyy-MM-dd HH:mm}</div>
</body></html>";

            return html;
        }

        public async Task<TodayStatsDto> GetTodayStatsAsync()
        {
            var ksaOffset = TimeSpan.FromHours(3);
            var ksaNow = DateTime.UtcNow + ksaOffset;
            var ksaDate = ksaNow.Date;
            var todayStart = ksaDate - ksaOffset;
            var todayEnd = ksaDate.AddDays(1) - ksaOffset;

            var todayQuery = (await ScopedAsync())
                .Where(a => a.action_at >= todayStart && a.action_at < todayEnd);

            return new TodayStatsDto
            {
                TodayLogins = await todayQuery.CountAsync(a => LoginActions.Contains(a.action!)),
                TodayStudentOps = await todayQuery.CountAsync(a => StudentActions.Contains(a.action!)),
                TodayRequestOps = await todayQuery.CountAsync(a => RequestActions.Contains(a.action!)),
                TodayFailedLogins = await todayQuery.CountAsync(a => a.action == "login_failed" || a.action == "login_admin_fallback_failed"),
                TodayDeletes = await todayQuery.CountAsync(a => a.action == "delete_student"),
                TodayActiveUsers = await todayQuery.Select(a => a.user_id).Distinct().CountAsync(),
                TodayTotalOps = await todayQuery.CountAsync()
            };
        }

        public async Task<List<AuditUserOptionDto>> GetUsersAsync()
        {
            // ⚠️ قائمة الفلترة تتبع نفس النطاق: بلا viewAll لا تظهر أسماء موظفي
            //    الإدارات الأخرى أصلًا. إخفاء صفوفهم وإبقاء أسمائهم في الفلتر
            //    يكشف نصف المعلومة ويجعل الشاشة تبدو معطّلة عند اختيارهم.
            var hidden = UnitOfWork.HasPermission(ApplicationPermissions.ViewAllAuditLogs.Value)
                ? new List<int>()
                : await HiddenActorIdsAsync();

            // نفس سبب ScopedAsync: قائمة كوسيط تعني OPENJSON ومنحة ذاكرة مبالغًا
            // فيها. العدد صغير، فالتصفية في الذاكرة بعد جلب الموظفين أرخص وأثبت.
            var hiddenSet = hidden.ToHashSet();
            var all = await _users.Query().AsNoTracking()
                .Where(u => u.is_active)
                .Select(u => new AuditUserOptionDto
                {
                    Id = u.Id,
                    Name = u.full_name ?? u.UserName
                })
                .OrderBy(u => u.Name)
                .ToListAsync();

            return all.Where(u => !hiddenSet.Contains(u.Id)).ToList();
        }

        // ----------------------------- Helpers -----------------------------

        private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> query, AuditLogFilter f)
        {
            if (f.UserId.HasValue)
                query = query.Where(a => a.user_id == f.UserId.Value);

            if (!string.IsNullOrEmpty(f.Action))
                query = query.Where(a => a.action == f.Action);

            if (!string.IsNullOrEmpty(f.ActionGroup))
            {
                var actions = f.ActionGroup.ToLower() switch
                {
                    "login" => new[] { "login", "login_admin_fallback", "login_admin_fallback_failed", "login_failed" },
                    "student" => StudentActions,
                    "request" => RequestActions,
                    "password" => new[] { "set_password" },
                    "logout" => new[] { "logout" },
                    "ad" => new[] { "user_created_ad", "user_updated_ad" },
                    _ => Array.Empty<string>()
                };
                if (actions.Length > 0)
                    query = query.Where(a => actions.Contains(a.action!));
            }

            if (DateTime.TryParse(f.FromDate, out var from))
                query = query.Where(a => a.action_at >= from);

            if (DateTime.TryParse(f.ToDate, out var to))
                query = query.Where(a => a.action_at <= to);

            if (!string.IsNullOrEmpty(f.Search))
            {
                var s = f.Search.ToLower();
                query = query.Where(a =>
                    (a.User != null && (
                        a.User.full_name != null && a.User.full_name.ToLower().Contains(s) ||
                        a.User.UserName != null && a.User.UserName.ToLower().Contains(s))) ||
                    (a.action != null && a.action.ToLower().Contains(s)) ||
                    (a.target_table != null && a.target_table.ToLower().Contains(s)) ||
                    a.target_id.ToString().Contains(s) ||
                    (a.ip_address != null && a.ip_address.Contains(s)));
            }

            return query;
        }
    }
}
