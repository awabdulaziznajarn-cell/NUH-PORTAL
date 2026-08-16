using System.Text.RegularExpressions;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Services
{
    // قراءة وكتابة أسماء حسابات وحدات سكن أعضاء هيئة التدريس.
    //
    // ⚠️ المكان الوحيد اللي بيعرف شكل الاسم. أي شاشة أو خدمة محتاجة تفكّ اسم
    //    أو تركّبه بتنادي هنا — عشان لو المعيار اتغيّر يتغيّر في ملف واحد.
    //
    // المعيار المتفق عليه:
    //    الأبراج : bu{رقم البرج}ap{رقم الشقة بخانتين}   →  bu10ap04
    //    الفلل   : villa{الرقم بخانتين}                 →  villa08
    //
    // ⚠️ الموجود فعلًا في الدومين فيه مخالفات، والقراءة بتقبلها عن قصد:
    //      ba8ap08   → البادئة "ba" مش "bu"
    //      villa019  → تلات خانات لرقم أقل من ١٠٠
    //    لو رفضناها كانت الوحدتين دول هيبانوا كأنهم مش موجودين في الدومين
    //    وهيتعملهم حسابات جديدة فوق حسابات شغّالة. بنقراهم وبنعلّمهم
    //    (matchesStandard = false) عشان يتصلّحوا بقرار واضح مش بصمت.
    public static class FacultyAccountNaming
    {
        // b + (u|a) عشان نقبل الغلطة الموجودة، والمجموعة بتقول أنهي بادئة اتقرت
        private static readonly Regex TowerRx =
            new(@"^b(?<p>[ua])(?<t>\d{1,2})ap(?<a>\d{1,3})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VillaRx =
            new(@"^villa(?<v>\d{1,4})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public const string TowerPrefix = "bu";
        public const string VillaPrefix = "villa";

        public sealed class ParsedAccount
        {
            public FacultyUnitType UnitType { get; init; }
            public int? TowerNo { get; init; }
            public int? ApartmentNo { get; init; }
            public int? VillaNo { get; init; }
            // الاسم مطابق للمعيار حرفيًا؟ false = بيشتغل عادي بس محتاج مراجعة
            public bool MatchesStandard { get; init; }
            // سبب المخالفة بالعربي — بيتعرض في شاشة «أسماء مخالفة للمعيار»
            public string? Deviation { get; init; }
        }

        // بيرجّع null لو الاسم مش من أسماء وحدات السكن أصلًا (حساب تاني في نفس الـ OU)
        public static ParsedAccount? TryParse(string? account)
        {
            if (string.IsNullOrWhiteSpace(account)) return null;
            var s = account.Trim();

            var t = TowerRx.Match(s);
            if (t.Success)
            {
                var prefix = t.Groups["p"].Value.ToLowerInvariant();
                var towerRaw = t.Groups["t"].Value;
                var aptRaw = t.Groups["a"].Value;

                var issues = new List<string>();
                if (prefix != "u") issues.Add($"البادئة «b{prefix}» بدل «bu»");
                if (aptRaw.Length != 2) issues.Add($"رقم الشقة «{aptRaw}» مش بخانتين");
                if (towerRaw.Length > 1 && towerRaw[0] == '0') issues.Add($"رقم البرج «{towerRaw}» فيه صفر زايد");

                return new ParsedAccount
                {
                    UnitType = FacultyUnitType.Tower,
                    TowerNo = int.Parse(towerRaw),
                    ApartmentNo = int.Parse(aptRaw),
                    MatchesStandard = issues.Count == 0,
                    Deviation = issues.Count == 0 ? null : string.Join(" · ", issues)
                };
            }

            var v = VillaRx.Match(s);
            if (v.Success)
            {
                var raw = v.Groups["v"].Value;
                var num = int.Parse(raw);
                // الرقم أقل من ١٠٠ لازم يبقى خانتين، ومن ١٠٠ لفوق تلاتة
                var expectedLen = num < 100 ? 2 : 3;

                var issues = new List<string>();
                if (raw.Length != expectedLen)
                    issues.Add($"الرقم «{raw}» بـ{raw.Length} خانات والمفروض {expectedLen}");
                if (num == 0) issues.Add("رقم الفيلا صفر");

                return new ParsedAccount
                {
                    UnitType = FacultyUnitType.Villa,
                    VillaNo = num,
                    MatchesStandard = issues.Count == 0,
                    Deviation = issues.Count == 0 ? null : string.Join(" · ", issues)
                };
            }

            return null;
        }

        // بيتستخدم للوحدات الجديدة بس. الوحدات المستوردة بتحتفظ باسمها الفعلي
        // من الدومين حتى لو مخالف — إعادة التسمية قرار منفصل ومقصود.
        public static string FormatTower(int towerNo, int apartmentNo)
            => $"{TowerPrefix}{towerNo}ap{apartmentNo:00}";

        public static string FormatVilla(int villaNo)
            => villaNo < 100 ? $"{VillaPrefix}{villaNo:00}" : $"{VillaPrefix}{villaNo}";
    }
}
