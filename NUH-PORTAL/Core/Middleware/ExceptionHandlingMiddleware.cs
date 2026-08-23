using Microsoft.Extensions.DependencyInjection;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using System.Security.Claims;

namespace NUH_PORTAL.Core.Middleware
{
    // بيترجم UserFriendlyException لرد JSON نظيف، وأي استثناء تاني لـ 500 من غير تسريب تفاصيل.
    // في وضع الـ Development بس: بنرجّع سبب الخطأ الحقيقي في حقل detail لتسريع التشخيص.
    // الأخطاء غير المتوقعة (500) بتتسجّل كمان في ErrorLogs (سجل الأخطاء) للتشخيص لاحقًا.
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (UserFriendlyException ex)
            {
                // ⚠️ كان بيعدّي من غير تسجيل مهما كانت الحالة، والنتيجة إن أعطال
                //    النظام الحقيقية كانت بتضيع: «فشل إنشاء حساب الشبكة» بيرجع
                //    للمستخدم ٥٠٠ ومايدخلش سجل الأخطاء خالص، فمفيش أثر يتشخّص منه.
                //
                //  ⚠️ الكود هو اللي بيقرّر، لا نوع الاستثناء: 4xx نتيجة طبيعية
                //     يشوفها المستخدم (بيانات ناقصة، غير موجود، تعارض) وماتتسجّلش،
                //     و5xx عطل في النظام اتلفّ في رسالة مقروءة — وده بالظبط اللي
                //     سجل الأخطاء موجود عشانه.
                if (ex.StatusCode >= 500)
                {
                    _logger.LogError(ex, "System failure surfaced as UserFriendlyException");
                    await PersistErrorAsync(context, ex, ex.StatusCode);
                }

                if (context.Response.HasStarted) throw;
                context.Response.StatusCode = ex.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new { message = ex.Message });
            }
            // ⚠️ تعارض نسخة: حد تاني عدّل نفس السجل بين قراءتنا وحفظنا.
            //    ده مش عطل — ده الحارس شغّال، ومنع الكتابة فوق تعديل التاني.
            //    ٤٠٩ لا ٥٠٠، ورسالة تقول للمستخدم يعمل إيه بالظبط.
            //    ومكانها هنا لا في كل خدمة: أي مسار بيحفظ بيستفيد منها.
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrent update rejected on {Path}", context.Request.Path);
                if (context.Response.HasStarted) throw;
                context.Response.StatusCode = 409;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "تم تعديل هذا السجل من مستخدم آخر أثناء عملك عليه. حدِّث الصفحة وراجع الحالة قبل إعادة المحاولة."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");

                // نسجّل الخطأ في قاعدة البيانات (سجل الأخطاء) — من غير ما نكسر الرد لو التسجيل نفسه فشل
                await PersistErrorAsync(context, ex, 500);

                if (context.Response.HasStarted) throw;
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json; charset=utf-8";

                if (_env.IsDevelopment())
                {
                    var root = ex.GetBaseException();
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "حدث خطأ غير متوقع، برجاء المحاولة لاحقًا",
                        detail = root.GetType().Name + ": " + root.Message
                    });
                }
                else
                {
                    await context.Response.WriteAsJsonAsync(new { message = "حدث خطأ غير متوقع، برجاء المحاولة لاحقًا" });
                }
            }
        }

        // بنستخدم scope جديد (DbContext نظيف) عشان ماننفعش نكتب على سياق ممكن يكون اتلوّث بنفس الخطأ.
        private static async Task PersistErrorAsync(HttpContext context, Exception ex, int statusCode)
        {
            try
            {
                var scopeFactory = context.RequestServices.GetService<IServiceScopeFactory>();
                if (scopeFactory == null) return;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<AppDbContext>();
                if (db == null) return;

                var root = ex.GetBaseException();

                // ⚠️ الرسالة الأصلية إنجليزية ومكتوبة لمطوّر يعرف السياق. بنضيف
                //    قبلها سطرًا عربيًا يقول «ده معناه إيه» — الشرح كله في مكان
                //    واحد (RequestDiagnostics.ExplainException) مش متفرّق هنا وهناك.
                var arabic = NUH_PORTAL.Core.Diagnostics.RequestDiagnostics.ExplainException(ex);
                var message = string.IsNullOrEmpty(arabic)
                    ? root.Message
                    : arabic + "\r\n\r\n[الرسالة الأصلية] " + root.Message;

                int? userId = null;
                var uidStr = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(uidStr, out var u) && u > 0) userId = u;

                db.ErrorLogs.Add(new ErrorLog
                {
                    occurred_at = DateTime.UtcNow,
                    message = Truncate(message, 2000),
                    exception_type = root.GetType().FullName,
                    stack_trace = Truncate(ex.ToString(), 8000),
                    source = Truncate(root.Source, 256),
                    request_path = Truncate(context.Request.Path.Value, 512),
                    request_method = context.Request.Method,
                    status_code = statusCode,
                    user_id = userId,
                    username = context.User?.Identity?.Name,
                    ip_address = context.Connection.RemoteIpAddress?.ToString(),
                    user_agent = context.Request.Headers.UserAgent.ToString()
                });
                await db.SaveChangesAsync();
            }
            catch
            {
                // متعمّد: تسجيل الخطأ best-effort — أي فشل هنا ماينفعش يخفي الخطأ الأصلي.
            }
        }

        private static string? Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
    }
}
