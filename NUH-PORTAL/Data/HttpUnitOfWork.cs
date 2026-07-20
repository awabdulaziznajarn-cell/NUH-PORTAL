using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace NUH_PORTAL.Data
{
    // نسخة مربوطة بالـ HTTP: بتقرأ id + role المستخدم الحالي من الـ JWT claims
    public class HttpUnitOfWork : UnitOfWork
    {
        public HttpUnitOfWork(AppDbContext context, IHttpContextAccessor httpAccessor) : base(context)
        {
            var user = httpAccessor.HttpContext?.User;
            var id = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
            if (int.TryParse(id, out var userId))
                CurrentUserId = userId;

            CurrentUserRole = user?.FindFirst(ClaimTypes.Role)?.Value?.Trim();
        }
    }
}
