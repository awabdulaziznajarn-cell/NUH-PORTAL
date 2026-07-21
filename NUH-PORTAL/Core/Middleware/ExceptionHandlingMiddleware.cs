using NUH_PORTAL.Core.Exceptions;

namespace NUH_PORTAL.Core.Middleware
{
    // بيترجم UserFriendlyException لرد JSON نظيف، وأي استثناء تاني لـ 500 من غير تسريب تفاصيل.
    // في وضع الـ Development بس: بنرجّع سبب الخطأ الحقيقي في حقل detail لتسريع التشخيص.
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
                if (context.Response.HasStarted) throw;
                context.Response.StatusCode = ex.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
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
    }
}
