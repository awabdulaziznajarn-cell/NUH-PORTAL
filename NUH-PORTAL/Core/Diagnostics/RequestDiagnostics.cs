using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using System.Security.Claims;

namespace NUH_PORTAL.Core.Diagnostics
{
    // ========================================================================
    //  تشخيص المشاكل بالعربي — مكان واحد يترجم أعراض النظام إلى كلام مفهوم.
    //
    //  ⚠️ ليه الملف ده موجود:
    //     ظهرت مشكلة بطء استغرق تشخيصها يومًا كاملًا: شاشات بتفتح في ٢٥ و٥٠
    //     ثانية، وكل النداءات ترجع 200 بلا أي خطأ. سجل الأخطاء كان فاضيًا —
    //     لأنه ما بيسجّل غير الاستثناءات، والبطء مش استثناء.
    //
    //     والسبب لما اتكشف كان: استعلام بيطلب من SQL Server منحة ذاكرة ضخمة
    //     مبنية على تقدير خاطئ، فيحجز الحوض ويخلّي كل استعلام تاني في الطابور
    //     (RESOURCE_SEMAPHORE). حاجة ما كانش فيه أي طريقة نشوفها من الواجهة.
    //
    //     فالقاعدة هنا: أي طلب بيتجاوز الحد المسموح بيتسجّل في **سجل الأخطاء**
    //     برسالة عربية تقول إيه اللي حصل وإيه السبب المرجّح — بدل ما المستخدم
    //     يقول «في بطء» ونفضل نخمّن.
    //
    //  ⚠️ الترجمة العربية للأعطال الفنية كمان في نفس الملف (ExplainException)،
    //     عشان ما تتفرقش في مكانين وتفترق مع أول تعديل.
    // ========================================================================
    public static class RequestDiagnostics
    {
        // النوع المكتوب في عمود «النوع» بالشاشة — ثابت عشان الفلترة، والشرح عربي.
        public const string SlowRequestType = "بطء في الاستجابة";

        // ⚠️ منع الإغراق: مسار بطيء ممكن يتكرر عشرات المرات في دقيقة (المستخدم
        //    بيضغط تاني وتاني). تسجيل واحد لكل مسار كل دقيقة يكفي للتشخيص
        //    وما بيغرقش الشاشة.
        private const string ThrottlePrefix = "diag::slow::";
        private static readonly TimeSpan ThrottleFor = TimeSpan.FromMinutes(1);

        // ====================================================================
        //  تسجيل طلب بطيء
        // ====================================================================
        public static async Task RecordSlowRequestAsync(HttpContext context, double elapsedMs, int thresholdMs)
        {
            try
            {
                var path = context.Request.Path.Value ?? "";

                // الملفات الثابتة مش شغلنا — بطؤها مسألة شبكة لا تطبيق
                if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains('.')) return;

                var cache = context.RequestServices.GetService<IMemoryCache>();
                if (cache != null)
                {
                    var key = ThrottlePrefix + path.ToLowerInvariant();
                    if (cache.TryGetValue(key, out _)) return;
                    cache.Set(key, true, ThrottleFor);
                }

                var scopeFactory = context.RequestServices.GetService<IServiceScopeFactory>();
                if (scopeFactory == null) return;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<AppDbContext>();
                if (db == null) return;

                var seconds = (elapsedMs / 1000.0).ToString("F1");
                var waiting = await CountQueriesWaitingForMemoryAsync(db);

                var reason = waiting switch
                {
                    > 0 => $"وفي نفس اللحظة كان فيه {waiting} استعلام منتظر ذاكرة من قاعدة البيانات "
                         + "(RESOURCE_SEMAPHORE). معناه إن استعلامًا واحدًا طلب منحة ذاكرة كبيرة "
                         + "وحجز الحوض، فوقف باقي الاستعلامات في الطابور. دوّر على استعلام خطته "
                         + "مبنية على تقدير صفوف خاطئ (قوائم تُمرَّر كوسيط فتُترجَم إلى OPENJSON، "
                         + "أو تحميل مجموعات مرتبطة في استعلام واحد).",

                    0 => "ولم يكن هناك استعلام منتظر ذاكرة في قاعدة البيانات وقتها، "
                       + "فالتأخير غالبًا في التطبيق نفسه أو في انتظار خدمة خارجية "
                       + "(الدليل النشط مثلًا) لا في قاعدة البيانات.",

                    _ => "وتعذّر فحص حالة ذاكرة قاعدة البيانات (يحتاج صلاحية VIEW SERVER STATE "
                       + "لحساب الخدمة)، فلا نعرف إن كان السبب من قاعدة البيانات."
                };

                int? userId = null;
                var uid = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(uid, out var u) && u > 0) userId = u;

                db.ErrorLogs.Add(new ErrorLog
                {
                    occurred_at = DateTime.UtcNow,
                    exception_type = SlowRequestType,
                    message = $"الطلب استغرق {seconds} ثانية (الحد المسموح "
                            + $"{(thresholdMs / 1000.0).ToString("F1")} ثانية). {reason}",
                    stack_trace =
                        "هذا ليس خطأً برمجيًا - الطلب نجح وأعاد النتيجة، لكنه تأخّر.\r\n"
                        + $"المسار: {context.Request.Method} {path}\r\n"
                        + $"المدة: {seconds} ثانية\r\n"
                        + $"حالة الرد: {context.Response.StatusCode}\r\n"
                        + $"استعلامات منتظرة للذاكرة وقتها: {(waiting < 0 ? "غير معروف" : waiting.ToString())}\r\n\r\n"
                        + "خطوات المتابعة:\r\n"
                        + "١) افتح أدوات المطوّر ← Network ← اضغط النداء ← ترويسة Server-Timing "
                        + "لمعرفة زمن الخادم وحده.\r\n"
                        + "٢) لو كان هناك انتظار للذاكرة، شغّل:\r\n"
                        + "   SELECT session_id, requested_memory_kb/1024.0 AS RequestedMB, grant_time\r\n"
                        + "   FROM sys.dm_exec_query_memory_grants ORDER BY requested_memory_kb DESC;\r\n"
                        + "   وأي استعلام يطلب مئات الميجابايت على جداول صغيرة هو المتهم.",
                    source = "RequestDiagnostics",
                    request_path = Truncate(path, 512),
                    request_method = context.Request.Method,
                    status_code = context.Response.StatusCode,
                    user_id = userId,
                    username = Truncate(context.User?.Identity?.Name, 256),
                    ip_address = context.Connection.RemoteIpAddress?.ToString(),
                    user_agent = Truncate(context.Request.Headers.UserAgent.ToString(), 512)
                });

                await db.SaveChangesAsync();
            }
            catch
            {
                // متعمّد: التشخيص best-effort ولا يجوز أن يكسر طلبًا نجح بالفعل.
            }
        }

        // كم استعلام واقف في طابور الذاكرة الآن. -1 = تعذّر الفحص (صلاحية ناقصة).
        // استعلام واحد صغير، ولا يُنفَّذ إلا على المسار البطيء — وهو نادر بطبيعته.
        private static async Task<int> CountQueriesWaitingForMemoryAsync(AppDbContext db)
        {
            try
            {
                var rows = await db.Database
                    .SqlQueryRaw<int>(
                        "SELECT COUNT(*) AS Value FROM sys.dm_exec_query_memory_grants WHERE grant_time IS NULL")
                    .ToListAsync();
                return rows.Count > 0 ? rows[0] : 0;
            }
            catch
            {
                return -1;
            }
        }

        // ====================================================================
        //  شرح عربي للأعطال الفنية المتكرّرة — يُضاف إلى رسالة الخطأ المسجَّلة.
        //
        //  ⚠️ الرسائل الأصلية إنجليزية ومكتوبة لمطوّر يعرف السياق. السطر العربي
        //     هنا بيقول للي بيقرا الشاشة: ده معناه إيه، وابدأ تدوّر فين.
        // ====================================================================
        public static string? ExplainException(Exception ex)
        {
            var root = ex.GetBaseException();
            var type = root.GetType().Name;
            var msg = root.Message ?? "";

            if (root is Microsoft.Data.SqlClient.SqlException sql)
            {
                return sql.Number switch
                {
                    -2 or 121 => "انتهت مهلة الاتصال بقاعدة البيانات. غالبًا استعلام طويل أو "
                               + "قفل على الجدول أو ضغط على الذاكرة - راجع سجل الأخطاء لطلبات بطيئة في نفس الوقت.",
                    2 or 53 or 10060 or 10061 =>
                        "تعذّر الوصول إلى خادم قاعدة البيانات (الاسم أو الشبكة أو الخدمة متوقفة).",
                    18456 => "فشل تسجيل الدخول إلى قاعدة البيانات - راجع بيانات الاتصال وصلاحيات حساب الخدمة.",
                    1205 => "تعارض أقفال (deadlock) وتم اختيار هذه العملية كضحية. أعد المحاولة، "
                          + "وإن تكرر فالسبب عمليتان تعدّلان نفس الجداول بترتيب مختلف.",
                    547 => "القيمة تخالف قيدًا في قاعدة البيانات (مرجع غير موجود أو حذف سجل مرتبط بغيره).",
                    2601 or 2627 => "القيمة مكرّرة في حقل لا يقبل التكرار.",
                    701 => "قاعدة البيانات لا تملك ذاكرة كافية لتنفيذ الاستعلام. "
                         + "على SQL Server Express السقف ١.٤ جيجابايت مهما كانت ذاكرة الجهاز.",
                    8645 or 8651 => "انتظار منحة ذاكرة في قاعدة البيانات - استعلام آخر يحجز الحوض.",
                    _ => $"خطأ من قاعدة البيانات (رقم {sql.Number})."
                };
            }

            if (type == "DbUpdateConcurrencyException")
                return "السجل تغيّر أو حُذف من مستخدم آخر أثناء التعديل.";

            if (type == "InvalidOperationException" && msg.Contains("second operation", StringComparison.OrdinalIgnoreCase))
                return "استُخدم نفس اتصال قاعدة البيانات في عمليتين متوازيتين - "
                     + "غالبًا نداءان متوازيان على نفس الخدمة بلا انتظار.";

            if (type == "TaskCanceledException" || type == "OperationCanceledException")
                return "أُلغيت العملية - إمّا المستخدم أغلق الصفحة أو انتهت مهلة الانتظار.";

            if (type.Contains("LdapException") || type.Contains("DirectoryServices"))
                return "تعذّر التخاطب مع الدليل النشط (Active Directory) - راجع اتصال وحدة التحكم بالنطاق.";

            return null;
        }

        private static string? Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
    }
}
