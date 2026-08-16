using Microsoft.AspNetCore.Http;
using NUH_PORTAL.Models.Enums;
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

            // الصلاحيات بتتحمّل في الكوكي/التوكن وقت الدخول (AuthService + TokenService)،
            // فبنقراها من نفس المكان اللي الـ policies بتقرا منه — مفيش استعلام قاعدة بيانات.
            if (user != null)
                foreach (var c in user.FindAll(Core.ClaimConstants.Permission))
                    if (!string.IsNullOrWhiteSpace(c.Value))
                        CurrentPermissions.Add(c.Value.Trim());

            // قسم الموظف (طلاب/طالبات) — نفس القصة: بيتحمّل في الـ claims من
            // PermissionClaimsTransformation مع كل طلب، فمفيش استعلام هنا.
            var scope = user?.FindFirst(Core.ClaimConstants.ScopeGender)?.Value?.Trim();
            if (scope == "male") CurrentScopeGender = Gender.Male;
            else if (scope == "female") CurrentScopeGender = Gender.Female;
        }
    }
}
