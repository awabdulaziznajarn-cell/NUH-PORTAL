using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Core
{
    // ========================================================================
    //  الصلاحيات تُقرأ من الدور في كل طلب، لا تُطبع في الكوكي أو التوكن.
    //
    //  ⚠️ العيب الذي عالجه هذا الملف:
    //     كانت صلاحيات الدور تُنسخ داخل الكوكي وداخل الـ JWT لحظة تسجيل الدخول
    //     ثم لا تتغيّر. النتائج التي ظهرت فعليًا:
    //
    //       • إضافة صلاحية جديدة لدور لا تصل لمن هو مسجَّل دخوله الآن — يظل
    //         يُرفَض بـ 403 حتى يخرج ويدخل من جديد.
    //       • سحب صلاحية من دور لا يُطبَّق فورًا — وهذه ثغرة أمنية لا مجرد إزعاج.
    //       • الكوكي والتوكن لهما عمران مختلفان، فالصفحة تفتح (كوكي حديث) بينما
    //         نداءات الـ API تُرفَض (توكن قديم) — نفس الشاشة تعمل مرة وترفض مرة.
    //
    //  الحل: IClaimsTransformation تعمل بعد كل عملية مصادقة ناجحة (كوكي أو JWT)
    //  فتحذف صلاحيات النسخة القديمة وتضع صلاحيات الدور الحالية من قاعدة البيانات.
    //  النتيجة: أي تعديل على صلاحيات دور يسري على الطلب التالي مباشرة، ومصدر
    //  الحقيقة الوحيد للصلاحيات هو جدول أدوار قاعدة البيانات.
    //
    //  التخزين المؤقت: 60 ثانية لكل دور. يمنع استعلامًا لكل طلب، ويظل التأخير
    //  الأقصى لسريان أي تعديل دقيقة واحدة — مقبول مقابل ألا نضرب القاعدة على
    //  كل نداء. (شاشة الأدوار تمسح الذاكرة عند الحفظ فيسري التعديل فورًا.)
    // ========================================================================
    public class PermissionClaimsTransformation : IClaimsTransformation
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

        public PermissionClaimsTransformation(RoleManager<Role> roleManager, IMemoryCache cache)
        {
            _roleManager = roleManager;
            _cache = cache;
        }

        public static string CacheKey(string roleName) => $"roleperms::{roleName.ToLowerInvariant()}";

        // تُستدعى من شاشة الأدوار بعد الحفظ حتى يسري التعديل بلا انتظار
        public static void Invalidate(IMemoryCache cache, string roleName) => cache.Remove(CacheKey(roleName));

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
                return principal!;

            var roles = principal.FindAll(ClaimTypes.Role)
                                 .Select(c => c.Value)
                                 .Where(v => !string.IsNullOrWhiteSpace(v))
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToList();

            if (roles.Count == 0)
                return principal;

            var fresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var role in roles)
                foreach (var p in await GetRolePermissionsAsync(role))
                    fresh.Add(p);

            // ⚠️ التحويل قد يُستدعى أكثر من مرة على نفس الطلب، فلا بد أن يكون
            //    idempotent: نحذف القديم أولًا ثم نضيف، وإلا تتضاعف الصلاحيات.
            var stale = identity.FindAll(ClaimConstants.Permission).ToList();
            foreach (var c in stale)
                identity.RemoveClaim(c);

            foreach (var p in fresh)
                identity.AddClaim(new Claim(ClaimConstants.Permission, p));

            return principal;
        }

        private async Task<IReadOnlyCollection<string>> GetRolePermissionsAsync(string roleName)
        {
            if (_cache.TryGetValue(CacheKey(roleName), out IReadOnlyCollection<string>? cached) && cached != null)
                return cached;

            var role = await _roleManager.FindByNameAsync(roleName);
            IReadOnlyCollection<string> perms;

            if (role == null)
            {
                // دور غير معروف (توكن قديم لدور محذوف مثلًا) → بلا صلاحيات.
                // منع افتراضي: لا نمرّر ما كان في التوكن.
                perms = Array.Empty<string>();
            }
            else
            {
                var claims = await _roleManager.GetClaimsAsync(role);
                perms = claims.Where(c => c.Type == ClaimConstants.Permission)
                              .Select(c => c.Value)
                              .Where(v => !string.IsNullOrWhiteSpace(v))
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToArray();
            }

            _cache.Set(CacheKey(roleName), perms, CacheFor);
            return perms;
        }
    }
}
