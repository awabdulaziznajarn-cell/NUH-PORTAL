using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace NUH_PORTAL.Data
{
    // نسخة مربوطة بالـ HTTP: بتقرأ id المستخدم الحالي من الـ JWT claims
    public class HttpUnitOfWork : UnitOfWork
    {
        public HttpUnitOfWork(AppDbContext context, IHttpContextAccessor httpAccessor) : base(context)
        {
            var id = httpAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (int.TryParse(id, out var userId))
                CurrentUserId = userId;
        }
    }
}
