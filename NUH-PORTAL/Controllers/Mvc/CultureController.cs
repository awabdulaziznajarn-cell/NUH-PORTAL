using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // تبديل لغة الواجهة — بيكتب كوكي الثقافة (.AspNetCore.Culture) ويرجّع المستخدم لنفس الصفحة.
    // من غير [Authorize] عشان يشتغل كمان على صفحة الدخول قبل تسجيل الدخول.
    [Route("Culture")]
    public class CultureController : Controller
    {
        private static readonly string[] Supported = { "ar", "en" };

        // GET /Culture/Set?culture=en&returnUrl=/Home
        [HttpGet("Set")]
        public IActionResult Set(string culture, string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(culture) && System.Array.IndexOf(Supported, culture) >= 0)
            {
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions
                    {
                        Expires = System.DateTimeOffset.UtcNow.AddYears(1),
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax
                    });
            }

            // حماية من open-redirect — نقبل الروابط المحلية بس
            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/Home";
            }

            return LocalRedirect(returnUrl);
        }
    }
}
