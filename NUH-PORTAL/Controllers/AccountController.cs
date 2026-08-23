using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services.Interfaces;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    // دخول/خروج المنصة — بيستخدم نفس IAuthService بتاع الـ API (نفس فلو AD ثم fallback بالحرف).
    // بعد النجاح: كوكي لصفحات الـ MVC + توكن JWT بنفس مفاتيح التخزين القديمة عشان الصفحات الحالية
    // تشتغل بنفس الجلسة — دخول واحد للعالمين القديم والجديد طول فترة التحويل.
    [Route("Account")]
    public class AccountController : Controller
    {
        // بعد ما لوحة التحكم بقت MVC — الدخول بيودّي على /Home (الصفحة القديمة dashboard.html لسه شغالة لحد ما نخلص التحويل)
        private const string DefaultRedirect = "/Home";

        private readonly IAuthService _auth;
        private readonly ITokenService _tokens;
        private readonly UserManager<User> _userManager;
        private readonly IPermissionService _permissions;

        public AccountController(IAuthService auth, ITokenService tokens, UserManager<User> userManager, IPermissionService permissions)
        {
            _auth = auth;
            _tokens = tokens;
            _userManager = userManager;
            _permissions = permissions;
        }

        // ====================================================================
        //  ⚠️ returnUrl جاي من الرابط — يعني من أي حد، مش من النظام.
        //     من غير الفحص ده الرابط ده يشتغل:
        //
        //        https://housing.nuh.edu.sa/Account/Login?returnUrl=https://<موقع-غريب>
        //
        //     الضحية بتشوف دومين الجامعة الحقيقي وصفحة الدخول الحقيقية، وبتدخل
        //     ببياناتها صح — وبعد نجاح الدخول النظام بنفسه بيرمي متصفحها على
        //     الموقع الغريب (صفحة دخول مقلّدة في العادة، بتقول «الجلسة انتهت،
        //     ادخل تاني»). مفيش أي علامة تخلّيها تشك: الرابط اللي وصلها كان
        //     دومينّا فعلًا.
        //
        //     وكان الفرق بين المسارين إن GET بيستخدم LocalRedirect (بتتحقّق
        //     وترمي استثناء)، وPOST بيحطّ الرابط في BridgeJson وصفحة LoginBridge
        //     بتنفّذه بـ location.replace بلا أي فحص — فالثغرة كانت في المسار
        //     اللي بيحصل بعد إدخال كلمة السر بالظبط.
        //
        //     Url.IsLocalUrl بترفض أي رابط مطلق أو بروتوكول أو //host وبتقبل
        //     المسارات الداخلية بس — نفس القاعدة المستعملة في CultureController.
        //     ومكتوبة هنا مرة واحدة عشان المسارين ما يفترقوش تاني.
        // ====================================================================
        private string SafeReturnUrl(string? returnUrl)
            => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : DefaultRedirect;

        // GET /Account/Login
        [AllowAnonymous]
        [HttpGet("Login")]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (existing.Succeeded)
                return LocalRedirect(SafeReturnUrl(returnUrl));

            // ⚠️ الفحص هنا كمان لا عند التحويل بس: القيمة دي بتترسم في حقل مخفي
            //    في النموذج وبترجع لينا مع POST. تنضيفها عند الدخول أنضف من
            //    الاعتماد على إن الفحص التاني هيمسكها.
            ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);
            return View();
        }

        // GET /Account/Denied
        // ----------------------------------------------------------------
        //  الصفحة دي مقصودة إنها متبقاش صفحة الدخول. لما AccessDeniedPath كان
        //  /Account/Login كان بيحصل لوب مقفول:
        //     صفحة محمية -> 403 -> /Account/Login -> الكوكي صالح فبيحوّل على
        //     /Home -> 403 تاني -> /Account/Login ...
        //  والمستخدم بيشوف ده كأنه "بيدخل ويطلع على طول"، فبيدوّر في اتجاه
        //  الجلسات والكوكي، والمشكلة أصلاً في الصلاحيات.
        //  هنا بنوقف اللوب ونعرض السبب الحقيقي: الدور وعدد صلاحياته.
        //  ⚠️ Policy = "signedIn" لا [Authorize] مجرّد: السياسة الافتراضية بقت
        //     بتطلب علامة «الحساب نشط»، والموظف الموقوف مالوش. ولو الصفحة دي
        //     رفضته، الرفض بيحوّله عليها هي نفسها — نفس اللوب المشروح فوق
        //     بالظبط، بس بسبب تاني.
        [Authorize(Policy = "signedIn")]
        [HttpGet("Denied")]
        public async Task<IActionResult> Denied()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
            var perms = await _permissions.GetPermissionsForRolesAsync(roles);

            ViewData["FullName"] = user?.full_name ?? User.Identity?.Name ?? "";
            ViewData["Roles"] = string.Join(", ", roles);
            ViewData["PermissionCount"] = perms.Count;
            return View();
        }

        // POST /Account/Login
        [AllowAnonymous]
        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            try
            {
                var user = await _auth.AuthenticateAsync(username, password);

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Count == 0) roles = new List<string> { "user" };
                var perms = await _permissions.GetPermissionsForRolesAsync(roles);

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.UserName ?? string.Empty),
                    new("FullName", user.full_name ?? user.UserName ?? string.Empty)
                };
                foreach (var r in roles)
                    claims.Add(new Claim(ClaimTypes.Role, (r ?? "").ToLowerInvariant()));
                foreach (var p in perms)
                    claims.Add(new Claim(ClaimConstants.Permission, p));

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity),
                    // IsPersistent = false ← كوكي جلسة: بتتمسح لما المتصفح يتقفل خالص.
                    // كانت true فالمستخدم كان بيرجع يلاقي نفسه داخل بآخر حساب دخل بيه،
                    // وde خطر على أي جهاز مشترك في مكتب الإسكان.
                    // مدة الخمول نفسها متحددة في Program.cs (ExpireTimeSpan) — مابنحطّش
                    // ExpiresUtc هنا عشان ما نعملش مدتين مختلفتين تتعارضوا.
                    new AuthenticationProperties
                    {
                        IsPersistent = false
                    });

                // توكن للصفحات القديمة (نفس شكل رد الـ API القديم بالحرف)
                var token = _tokens.GenerateToken(user, roles, perms);
                var bridge = new
                {
                    token,
                    user = new
                    {
                        id = user.Id,
                        username = user.UserName,
                        full_name = user.full_name,
                        role = roles.FirstOrDefault() ?? "user"
                    },
                    // ⚠️ SafeReturnUrl لا القيمة الخام: LoginBridge بتنفّذ القيمة دي
                    //    بـ location.replace من غير أي فحص من ناحيتها.
                    redirect = SafeReturnUrl(returnUrl)
                };
                ViewData["BridgeJson"] = System.Text.Json.JsonSerializer.Serialize(bridge);
                return View("LoginBridge");
            }
            catch (UserFriendlyException ex)
            {
                ViewData["Error"] = ex.Message;
                ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);
                return View();
            }
        }

        // POST /Account/Logout
        [HttpPost("Logout")]
        // ⚠️ Policy = "signedIn" عن قصد: الموظف الموقوف لازم يقدر يخرج ويمسح
        //    الكوكي بنفسه. لو السياسة الافتراضية طبّقت عليه، الخروج نفسه كان
        //    هيترفض ويفضل عالق في شاشة الرفض لحد ما يقفل المتصفح.
        [Authorize(Policy = "signedIn", AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Logout()
        {
            // اخرج (امسح كوكي الجلسة) الأول — ده الأهم. سجل الإجراء best-effort:
            // أي بطء/تايم-أوت في الداتابيز مايمنعش الخروج نفسه.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            try { await _auth.LogoutAsync(); } catch { /* سجل الخروج مش لازم يوقف الخروج */ }
            return Redirect("/Account/Login");
        }
    }
}
