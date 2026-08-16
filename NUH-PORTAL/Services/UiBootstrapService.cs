using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class UiBootstrapService : IUiBootstrapService
    {
        // مهم: JavaScriptEncoder.Create(UnicodeRanges.All) بيسيب الحروف العربية زي ما هي
        // لكنه *لسه* بيهرب < > & ' " — يعني آمن جوه <script> بالظبط زي الافتراضي.
        //
        // الافتراضي كان بيهرب كل حرف مش ASCII لـ \uXXXX، فالحرف العربي كان بياخد 6 بايت
        // بدل 2. النتيجة: قاموس الترجمة كان 142 كيلوبايت في كل صفحة بدل 71.
        //
        // ⚠️ ماتستبدلهاش بـ UnsafeRelaxedJsonEscaping — دي بتبطّل تهريب < و > فبتفتح
        //    ثغرة XSS لو نص ترجمة فيه </script>.
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private const string I18nKeyPrefix = "ui.i18n.";
        private const string LookupKeyPrefix = "ui.lookups.";
        private const string PortalKeyPrefix = "ui.portal.";

        // مفاتيح بوابة الطالب: pt_ للنصوص الخاصة بالبوابة، و reg_ / lk_ مشتركة مع
        // شاشة تسجيل طالب فردي عند الموظف — مصدر واحد للنص في الشاشتين.
        private static readonly string[] PortalPrefixes =
            { "pt_", "reg_", "lk_", "Page_", "Brand", "Lang", "AppTitle", "appTitle" };

        private readonly IMemoryCache _cache;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILookupService _lookups;

        public UiBootstrapService(
            IMemoryCache cache,
            IStringLocalizer<SharedResource> localizer,
            ILookupService lookups)
        {
            _cache = cache;
            _localizer = localizer;
            _lookups = lookups;
        }

        private static string CultureKey => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "ar";

        public string GetI18nJson()
        {
            var key = I18nKeyPrefix + CultureKey;
            if (_cache.TryGetValue<string>(key, out var cached) && cached != null)
                return cached;

            string json;
            try
            {
                var dict = new Dictionary<string, string>();
                foreach (var s in _localizer.GetAllStrings(true))
                    dict[s.Name] = s.Value;
                json = JsonSerializer.Serialize(dict, JsonOpts);
            }
            catch
            {
                // مورد ناقص مايوقّفش الصفحة — الواجهة بترجع للنص الافتراضي في t()
                return "{}";
            }

            // من غير انتهاء صلاحية: الموارد متجمّعة في الـ DLL ومبتتغيّرش وقت التشغيل.
            // Size مش مستخدم لأن IMemoryCache هنا من غير SizeLimit.
            _cache.Set(key, json);
            return json;
        }

        public async Task<string> GetPortalBootstrapJsonAsync()
        {
            var key = PortalKeyPrefix + CultureKey;
            if (_cache.TryGetValue<string>(key, out var cached) && cached != null)
                return cached;

            string i18n;
            try
            {
                var dict = new Dictionary<string, string>();
                foreach (var s in _localizer.GetAllStrings(true))
                {
                    foreach (var p in PortalPrefixes)
                    {
                        if (s.Name.StartsWith(p, StringComparison.Ordinal)) { dict[s.Name] = s.Value; break; }
                    }
                }
                i18n = JsonSerializer.Serialize(dict, JsonOpts);
            }
            catch
            {
                i18n = "{}";
            }

            var json = "{\"i18n\":" + i18n + ",\"lookups\":" + await GetLookupMapJsonAsync() + "}";

            // ستين ثانية زي خريطة القوائم — الجزء المتغيّر الوحيد جوّاه هو القوائم.
            _cache.Set(key, json, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            });
            return json;
        }

        public async Task<string> GetLookupMapJsonAsync()
        {
            var key = LookupKeyPrefix + CultureKey;
            if (_cache.TryGetValue<string>(key, out var cached) && cached != null)
                return cached;

            string json;
            try
            {
                var colleges = new Dictionary<string, string>();
                foreach (var it in await _lookups.GetCollegesAsync())
                {
                    var k = (it.Code ?? "").Trim().ToLowerInvariant();
                    if (k.Length > 0) colleges[k] = it.Name;
                }

                var departments = new Dictionary<string, string>();
                foreach (var it in await _lookups.GetDepartmentsAsync(null))
                {
                    var k = (it.Code ?? "").Trim().ToLowerInvariant();
                    if (k.Length > 0) departments[k] = it.Name;
                }

                var buildings = new Dictionary<string, string>();
                foreach (var it in await _lookups.GetBuildingsAsync(null))
                {
                    var k = (it.Code ?? "").Trim().ToLowerInvariant();
                    if (k.Length > 0) buildings[k] = it.Name;
                }

                var levels = new Dictionary<string, string>();
                foreach (var it in await _lookups.GetAcademicLevelsAsync())
                {
                    var k = (it.Code ?? "").Trim().ToLowerInvariant();
                    if (k.Length > 0) levels[k] = it.Name;
                }

                json = JsonSerializer.Serialize(
                    new { college = colleges, department = departments, building = buildings, level = levels },
                    JsonOpts);
            }
            catch
            {
                // جداول القوائم لسه ماتهاجرتش → {} زي السلوك القديم بالظبط، والصفحة بتفضل شغّالة
                return "{}";
            }

            _cache.Set(key, json, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            });
            return json;
        }
    }
}
