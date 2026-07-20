using System.Diagnostics;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuditLogsController> _logger;
        public AuditLogsController(AppDbContext context, ILogger<AuditLogsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] int? userId = null,
            [FromQuery] string? actionGroup = null,
            [FromQuery] string? action = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? search = null)
        {
            var sw = Stopwatch.StartNew();

            var baseQuery = _context.AuditLogs.AsNoTracking();

            var filteredQuery = ApplyFilters(baseQuery, userId, actionGroup, action, fromDate, toDate, search);

            var filterMs = sw.ElapsedMilliseconds;

            var totalRecords = await filteredQuery.CountAsync();
            var t1 = sw.ElapsedMilliseconds;

            var totalUsers = await filteredQuery.Select(a => a.user_id).Distinct().CountAsync();
            var t2 = sw.ElapsedMilliseconds;

            var studentOps = await filteredQuery.CountAsync(a =>
                a.action == "create_student" || a.action == "update_student" || a.action == "delete_student" || a.action == "checkout_student");
            var t3 = sw.ElapsedMilliseconds;

            var requestOps = await filteredQuery.CountAsync(a =>
                a.action == "create_request" || a.action == "approve_request" || a.action == "reject_request" ||
                a.action == "housing_approve_request" || a.action == "housing_reject_request" ||
                a.action == "submit_cyber_review" ||
                a.action == "cyber_approve_request" || a.action == "cyber_reject_request");
            var t4 = sw.ElapsedMilliseconds;

            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            var logs = await filteredQuery
                .OrderByDescending(a => a.action_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.user_id,
                    a.action,
                    a.target_table,
                    a.target_id,
                    a.action_at,
                    a.ip_address,
                    a.user_agent,
                    user_name = a.User != null ? (a.User.full_name ?? a.User.username) : null,
                    user = a.User != null
                        ? new { full_name = a.User.full_name, username = a.User.username }
                        : null,
                    changes = a.AuditChangeLogs != null
                        ? a.AuditChangeLogs.Select(c => new { c.FieldName, c.OldValue, c.NewValue }).ToList()
                        : null
                })
                .ToListAsync();
            var t5 = sw.ElapsedMilliseconds;

            var result = new
            {
                data = logs,
                page,
                pageSize,
                totalRecords,
                totalPages,
                stats = new
                {
                    totalRecords,
                    totalUsers,
                    studentOperations = studentOps,
                    requestOperations = requestOps
                }
            };
            var preSerialMs = sw.ElapsedMilliseconds;

            // Estimate response size
            var sizeEstimate = Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(result));

            var traceId = HttpContext.TraceIdentifier;

            Response.OnStarting(() =>
            {
                var totalMs = sw.ElapsedMilliseconds;
                var serialMs = totalMs - preSerialMs;

                long? contentLength = null;
                if (Response.Headers.TryGetValue("Content-Length", out var val))
                    contentLength = long.TryParse(val, out var parsed) ? parsed : null;

                Response.Headers["X-Diagnostics"] =
                    $"trace={traceId} filter={filterMs}ms q1={t1 - filterMs}ms q2={t2 - t1}ms q3={t3 - t2}ms q4={t4 - t3}ms q5={t5 - t4}ms build={preSerialMs - t5}ms serial={serialMs}ms total={totalMs}ms";

                _logger.LogWarning(
                    "AUDIT_LOGS_PERF trace={TraceId} " +
                    "size_est={Size}b content_length={CL} " +
                    "filter={Filter}ms q1_counts={Q1}ms q2_users={Q2}ms q3_students={Q3}ms q4_requests={Q4}ms q5_select={Q5}ms " +
                    "build={Build}ms serial={Serial}ms total={Total}ms " +
                    "rows={Rows}",
                    traceId, sizeEstimate, contentLength,
                    filterMs, t1 - filterMs, t2 - t1, t3 - t2, t4 - t3, t5 - t4,
                    preSerialMs - t5, serialMs, totalMs,
                    logs.Count);

                return Task.CompletedTask;
            });

            return Ok(result);
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportLogs(
            [FromQuery] int? userId = null,
            [FromQuery] string? actionGroup = null,
            [FromQuery] string? action = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? search = null)
        {
            var filteredQuery = ApplyFilters(_context.AuditLogs.AsNoTracking(), userId, actionGroup, action, fromDate, toDate, search);

            var logs = await filteredQuery
                .OrderByDescending(a => a.action_at)
                .Select(a => new
                {
                    a.Id,
                    user_name = a.User != null ? (a.User.full_name ?? a.User.username) : null,
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

            var fileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet("chart-data")]
        public async Task<IActionResult> GetChartData()
        {
            var now = DateTime.UtcNow;

            var sevenDaysAgo = now.AddDays(-7).Date;
            var thirtyDaysAgo = now.AddDays(-30).Date;

            var last7Days = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.action_at >= sevenDaysAgo)
                .GroupBy(a => a.action_at.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(x => x.date)
                .ToListAsync();

            var login30 = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.action_at >= thirtyDaysAgo &&
                    (a.action == "login" || a.action == "login_failed" || a.action == "logout"))
                .GroupBy(a => a.action_at.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(x => x.date)
                .ToListAsync();

            var studentOps = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.action_at >= sevenDaysAgo &&
                    (a.action == "create_student" || a.action == "update_student" || a.action == "delete_student"))
                .GroupBy(a => a.action)
                .Select(g => new { action = g.Key, count = g.Count() })
                .ToListAsync();

            var requestOps = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.action_at >= sevenDaysAgo &&
                    (a.action == "create_request" || a.action == "approve_request" || a.action == "reject_request" ||
                     a.action == "housing_approve_request" || a.action == "housing_reject_request" ||
                     a.action == "submit_cyber_review" ||
                     a.action == "cyber_approve_request" || a.action == "cyber_reject_request"))
                .GroupBy(a => a.action)
                .Select(g => new { action = g.Key, count = g.Count() })
                .ToListAsync();

            return Ok(new { last7Days, login30, studentOps, requestOps });
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            var now = DateTime.UtcNow;
            var alerts = new List<object>();

            var tenMinAgo = now.AddMinutes(-10);
            var thirtyMinAgo = now.AddMinutes(-30);

            var failedLogins = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.action == "login_failed" && a.action_at >= tenMinAgo)
                .CountAsync();
            if (failedLogins >= 5)
                alerts.Add(new { type = "failed_login_explosion", severity = "high", count = failedLogins, message_ar = "نشاط تسجيل دخول مشبوه", message_en = "Suspicious Login Activity" });

            var deletes = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.action == "delete_student" && a.action_at >= tenMinAgo)
                .CountAsync();
            if (deletes >= 5)
                alerts.Add(new { type = "excessive_deletes", severity = "high", count = deletes, message_ar = "حذف متكرر للطلاب", message_en = "Excessive Student Deletions" });

            var rejections = await _context.AuditLogs.AsNoTracking()
                .Where(a => (a.action == "reject_request" || a.action == "housing_reject_request" || a.action == "cyber_reject_request") && a.action_at >= thirtyMinAgo)
                .CountAsync();
            if (rejections >= 10)
                alerts.Add(new { type = "excessive_rejections", severity = "medium", count = rejections, message_ar = "عدد مرتفع من الطلبات المرفوضة", message_en = "High Request Rejection Volume" });

            return Ok(alerts);
        }

        [HttpGet("report-html")]
        public async Task<IActionResult> GetReportHtml(
            [FromQuery] string? type = "activity",
            [FromQuery] int? userId = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
        {
            var filteredQuery = ApplyFilters(_context.AuditLogs.AsNoTracking(), userId, null, null, fromDate, toDate, null);

            if (type == "login")
                filteredQuery = filteredQuery.Where(a =>
                    a.action == "login" || a.action == "login_failed" || a.action == "logout" ||
                    a.action == "login_admin_fallback" || a.action == "login_admin_fallback_failed");
            else if (type == "student")
                filteredQuery = filteredQuery.Where(a =>
                    a.action == "create_student" || a.action == "update_student" || a.action == "delete_student");
            else if (type == "request")
                filteredQuery = filteredQuery.Where(a =>
                    a.action == "create_request" || a.action == "approve_request" || a.action == "reject_request" ||
                    a.action == "housing_approve_request" || a.action == "housing_reject_request" ||
                    a.action == "submit_cyber_review" ||
                    a.action == "cyber_approve_request" || a.action == "cyber_reject_request");

            var logs = await filteredQuery
                .OrderByDescending(a => a.action_at)
                .Take(500)
                .Select(a => new
                {
                    user_name = a.User != null ? (a.User.full_name ?? a.User.username) : null,
                    a.action,
                    a.target_table,
                    a.target_id,
                    a.action_at,
                    a.ip_address
                })
                .ToListAsync();

            var lang = Request.Headers["Accept-Language"].ToString().StartsWith("ar") ? "ar" : "en";
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
            html += @"</tbody></table>
<div class='footer'>NUH Housing Portal — {DateTime.Now:yyyy-MM-dd HH:mm}</div>
</body></html>";

            return Content(html, "text/html;charset=utf-8");
        }

        [HttpGet("today-stats")]
        public async Task<IActionResult> GetTodayStats()
        {
            var ksaOffset = TimeSpan.FromHours(3);
            var ksaNow = DateTime.UtcNow + ksaOffset;
            var ksaDate = ksaNow.Date;
            var todayStart = ksaDate - ksaOffset;
            var todayEnd = ksaDate.AddDays(1) - ksaOffset;

            var todayQuery = _context.AuditLogs.AsNoTracking()
                .Where(a => a.action_at >= todayStart && a.action_at < todayEnd);

            var todayLogins = await todayQuery.CountAsync(a =>
                a.action == "login" || a.action == "logout" || a.action == "login_failed" ||
                a.action == "login_admin_fallback" || a.action == "login_admin_fallback_failed");

            var todayStudentOps = await todayQuery.CountAsync(a =>
                a.action == "create_student" || a.action == "update_student" || a.action == "delete_student" || a.action == "checkout_student");

            var todayRequestOps = await todayQuery.CountAsync(a =>
                a.action == "create_request" || a.action == "approve_request" || a.action == "reject_request" ||
                a.action == "housing_approve_request" || a.action == "housing_reject_request" ||
                a.action == "submit_cyber_review" ||
                a.action == "cyber_approve_request" || a.action == "cyber_reject_request");

            var todayFailedLogins = await todayQuery.CountAsync(a =>
                a.action == "login_failed" || a.action == "login_admin_fallback_failed");

            var todayDeletes = await todayQuery.CountAsync(a =>
                a.action == "delete_student");

            var todayActiveUsers = await todayQuery
                .Select(a => a.user_id).Distinct().CountAsync();

            var todayTotalOps = await todayQuery.CountAsync();

            return Ok(new
            {
                todayLogins,
                todayStudentOps,
                todayRequestOps,
                todayFailedLogins,
                todayDeletes,
                todayActiveUsers,
                todayTotalOps
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => u.is_active)
                .Select(u => new
                {
                    id = u.Id,
                    name = u.full_name ?? u.username
                })
                .OrderBy(u => u.name)
                .ToListAsync();

            return Ok(users);
        }

        private static IQueryable<AuditLog> ApplyFilters(
            IQueryable<AuditLog> query,
            int? userId,
            string? actionGroup,
            string? action,
            string? fromDate,
            string? toDate,
            string? search)
        {
            if (userId.HasValue)
                query = query.Where(a => a.user_id == userId.Value);

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.action == action);

            if (!string.IsNullOrEmpty(actionGroup))
            {
                var actions = actionGroup.ToLower() switch
                {
                    "login" => new[] { "login", "login_admin_fallback", "login_admin_fallback_failed", "login_failed" },
                    "student" => new[] { "create_student", "update_student", "delete_student", "checkout_student" },
                    "request" => new[] { "create_request", "approve_request", "reject_request",
                        "housing_approve_request", "housing_reject_request",
                        "submit_cyber_review",
                        "cyber_approve_request", "cyber_reject_request" },
                    "password" => new[] { "set_password" },
                    "logout" => new[] { "logout" },
                    "ad" => new[] { "user_created_ad", "user_updated_ad" },
                    _ => Array.Empty<string>()
                };
                if (actions.Length > 0)
                    query = query.Where(a => actions.Contains(a.action!));
            }

            if (DateTime.TryParse(fromDate, out var from))
                query = query.Where(a => a.action_at >= from);

            if (DateTime.TryParse(toDate, out var to))
                query = query.Where(a => a.action_at <= to);

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(a =>
                    (a.User != null && (
                        a.User.full_name != null && a.User.full_name.ToLower().Contains(s) ||
                        a.User.username != null && a.User.username.ToLower().Contains(s))) ||
                    (a.action != null && a.action.ToLower().Contains(s)) ||
                    (a.target_table != null && a.target_table.ToLower().Contains(s)) ||
                    a.target_id.ToString().Contains(s) ||
                    (a.ip_address != null && a.ip_address.Contains(s)));
            }

            return query;
        }
    }
}