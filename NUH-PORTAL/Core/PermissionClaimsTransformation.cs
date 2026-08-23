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
    //
    //  ⚠️ والعيب التاني اللي عالجه الملف (٢.٣): إيقاف الموظف ماكانش بيقطع جلسته.
    //     التوكن عمره ٨ ساعات ومتخزّن في المتصفح، وفحص is_active/is_deleted كان
    //     في مسار الدخول وحده — يعني الموظف اللي بيتوقف الساعة ٩ الصبح يفضل
    //     شغّال على النظام بكامل صلاحياته لحد الساعة ٥. وده بالظبط الوقت اللي
    //     بيتوقف فيه الحساب لسبب: نهاية تعاقد، نقل، أو حادثة أمنية.
    //
    //     المعالجة هنا لا في مسار الدخول: حالة الحساب بتتقرأ من القاعدة مع كل
    //     طلب (بنفس كاش الدقيقة)، ولو الحساب موقوف أو محذوف بتتشال كل الأدوار
    //     وكل الصلاحيات من الهوية. التوكن يفضل صالح تقنيًا لكنه بقى بلا أي
    //     سلطة — أي صفحة أو نداء محمي بيرجع ٤٠٣، والخروج بيحصل عمليًّا.
    //     وشاشة المستخدمين بتمسح الكاش عند الإيقاف/الحذف/الاستعادة فيسري فورًا.
    // ========================================================================
    public class PermissionClaimsTransformation : IClaimsTransformation
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

        public PermissionClaimsTransformation(RoleManager<Role> roleManager, UserManager<User> userManager, IMemoryCache cache)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _cache = cache;
        }

        // ⚠️ حالة الحساب والقسم في سطر واحد مخزّن: الاتنين بيتقروا من نفس صفّ
        //    المستخدم في نفس اللحظة، فتخزينهم منفصلين كان معناه استعلامين
        //    وكاشين ممكن يفترقوا — واحد يقول «موقوف» والتاني لسه شايف قسمه.
        private sealed record UserState(bool Active, string Scope);

        public static string CacheKey(string roleName) => $"roleperms::{roleName.ToLowerInvariant()}";
        private static string StateCacheKey(int userId) => $"userstate::{userId}";

        // تُستدعى بعد أي تغيير على المستخدم من شاشة المستخدمين (القسم، الإيقاف،
        // الحذف، الاستعادة) حتى يسري فورًا بدل انتظار انتهاء الكاش
        public static void InvalidateUser(IMemoryCache cache, int userId) => cache.Remove(StateCacheKey(userId));

        // تُستدعى من شاشة الأدوار بعد الحفظ حتى يسري التعديل بلا انتظار
        public static void Invalidate(IMemoryCache cache, string roleName) => cache.Remove(CacheKey(roleName));

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
                return principal!;

            // ⚠️ المسح الأول دايمًا، قبل أي قرار. سببان:
            //    • التحويل ممكن يتنادى أكتر من مرة على نفس الطلب، فلو ضفنا من غير
            //      مسح الصلاحيات بتتضاعف.
            //    • المنع الافتراضي: لو حصل أي خروج مبكر تحت، الهوية بتكون خلاص
            //      اتجرّدت من نسخة الكوكي/التوكن القديمة — مش محتفظة بيها.
            foreach (var c in identity.FindAll(ClaimConstants.ScopeGender).ToList())
                identity.RemoveClaim(c);
            foreach (var c in identity.FindAll(ClaimConstants.Permission).ToList())
                identity.RemoveClaim(c);
            // ⚠️ دي بالذات لازم تتشال هنا: لو الكوكي أو التوكن جاي بيها من قبل
            //    الإيقاف، سيبانها معناه إن الموظف الموقوف يعدّي من السياسة
            //    الافتراضية. العلامة تتحطّ من القاعدة كل طلب لا تتوارث.
            foreach (var c in identity.FindAll(ClaimConstants.AccountActive).ToList())
                identity.RemoveClaim(c);

            var state = await GetUserStateAsync(principal);

            // ⚠️ الحساب موقوف أو محذوف: التوكن لسه صالح تقنيًا (عمره ٨ ساعات) لكن
            //    بنشيل منه كل دور وكل صلاحية، فيبقى بلا أي سلطة على النظام.
            //    ماينفعش نستنى انتهاء التوكن: ده بالظبط العيب اللي بنقفله.
            if (state is { Active: false })
            {
                foreach (var c in identity.FindAll(ClaimTypes.Role).ToList())
                    identity.RemoveClaim(c);
                return principal;
            }

            // ⚠️ وصلنا هنا يبقى الحساب موجود ونشط وغير محذوف — دي العلامة اللي
            //    بتفتح السياسة الافتراضية. من غيرها كل [Authorize] بلا صلاحية
            //    (لوحة التحكم، الإشعارات، سجلات النظام...) بيترفض.
            if (state != null)
                identity.AddClaim(new Claim(ClaimConstants.AccountActive, "1"));

            // ⚠️ قسم الموظف (طلاب/طالبات) بيتحمّل هنا مش وقت الدخول — لنفس سبب
            //    الصلاحيات بالظبط. لو اتحفظ في الكوكي وقت الدخول، المسؤول يغيّر قسم
            //    المشرفة من الشاشة وما يحصلش حاجة، وتفضل شايفة القسم القديم لحد ما
            //    تخرج وتدخل — وهي مش عارفة إن ده مطلوب أصلًا.
            if (state != null && !string.IsNullOrEmpty(state.Scope))
                identity.AddClaim(new Claim(ClaimConstants.ScopeGender, state.Scope));

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

            foreach (var p in fresh)
                identity.AddClaim(new Claim(ClaimConstants.Permission, p));

            return principal;
        }

        private async Task<UserState?> GetUserStateAsync(ClaimsPrincipal principal)
        {
            var idText = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idText, out var userId) || userId <= 0) return null;

            if (_cache.TryGetValue(StateCacheKey(userId), out UserState? cached) && cached != null)
                return cached;

            var user = await _userManager.FindByIdAsync(userId.ToString());

            // ⚠️ المستخدم مش موجود في القاعدة أصلًا وهو ماسك توكن → موقوف.
            //    مفيش هوية في النظام غير اللي بتتعمل من صفّ مستخدم حقيقي
            //    (AccountController والـ JWT)، فالحالة دي معناها صفّ اتشال.
            var state = user == null
                ? new UserState(false, "")
                : new UserState(
                    user.is_active && !user.is_deleted,
                    user.scope_gender switch
                    {
                        Models.Enums.Gender.Male => "male",
                        Models.Enums.Gender.Female => "female",
                        _ => ""
                    });

            _cache.Set(StateCacheKey(userId), state, CacheFor);
            return state;
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
