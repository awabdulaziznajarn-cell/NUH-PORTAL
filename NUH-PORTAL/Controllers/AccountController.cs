using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Core.Exceptions;
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

        public AccountController(IAuthService auth, ITokenService tokens)
        {
            _auth = auth;
            _tokens = tokens;
        }

        // GET /Account/Login
        [AllowAnonymous]
        [HttpGet("Login")]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (existing.Succeeded)
                return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? DefaultRedirect : returnUrl);

            ViewData["ReturnUrl"] = returnUrl;
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

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.username ?? string.Empty),
                    new(ClaimTypes.Role, (user.role ?? "user").ToLowerInvariant()),
                    new("FullName", user.full_name ?? user.username ?? string.Empty)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                // توكن للصفحات القديمة (نفس شكل رد الـ API القديم بالحرف)
                var token = _tokens.GenerateToken(user);
                var bridge = new
                {
                    token,
                    user = new
                    {
                        id = user.Id,
                        username = user.username,
                        full_name = user.full_name,
                        role = user.role
                    },
                    redirect = string.IsNullOrEmpty(returnUrl) ? DefaultRedirect : returnUrl
                };
                ViewData["BridgeJson"] = System.Text.Json.JsonSerializer.Serialize(bridge);
                return View("LoginBridge");
            }
            catch (UserFriendlyException ex)
            {
                ViewData["Error"] = ex.Message;
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }
        }

        // POST /Account/Logout
        [HttpPost("Logout")]
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Logout()
        {
            await _auth.LogoutAsync();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/Account/Login");
        }
    }
}
