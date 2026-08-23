using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  توثيق التعهّد — التعريف الوحيد في النظام.
    //
    //  المشكلة اللي الملف ده بيحلّها:
    //     التعهّد كان بيتسجّل من كلام العميل بالكامل. الواجهة بتبعت
    //     declarationAccepted:true و policyAccepted:true و policyVersion:"1.0"،
    //     والخادم بياخدهم زي ما هم ويحطّهم في الجدول. يعني السجل بيقول
    //     «الطالب وافق» من غير ما يعرف **على إيه** وافق ولا يقدر يثبته بعدين.
    //     وده بلا قيمة لو حصل خلاف: البنود بيعدّلها المدير من شاشة القوائم
    //     المرجعية في أي وقت، فنصّ اليوم مش نصّ الشهر اللي فات، والسجل
    //     مافيهوش نسخة النصّ اللي الطالب شافه.
    //
    //     وكمان: صفحة الإقرار مكانتش بتنادي الـ API أصلًا — كانت بتكتب
    //     declAccepted في تخزين المتصفح وتنتقل للخطوة اللي بعدها. فجدول
    //     StudentDeclarations كان بيفضل فاضي مهما اتقدّم طلبات.
    //
    //  الحلّ في نقطتين:
    //
    //     (أ) تجميد النصّ + بصمة: الخادم بيبني نصّ البنود من الجدول وقت
    //         الموافقة، ويحسب SHA-256 عليه، ويخزّن **النصّ والبصمة** مع
    //         السجل. أي تعديل في البنود بعد كده بيغيّر البصمة، والسجل القديم
    //         بيفضل شاهد على نصّه هو.
    //
    //     (د) إقرار مكتوب: الطالب بيكتب جملة الإقرار بخطّ إيده. علامة «✓»
    //         ممكن تتحطّ بضغطة غير مقصودة أو بسكربت، لكن جملة مكتوبة حرف
    //         بحرف فعل إرادي. والتحقّق منها **على الخادم** — لأن أي فحص في
    //         المتصفح بيتشال من أدوات المطوّر في ثانية.
    //
    //  ⚠️ والقاعدة مكتوبة هنا مرة واحدة: نصّ الجملة والتطبيع والبصمة. الواجهة
    //     بتاخد نفس القواعد متولّدة منها على /js/nuh-pledge.js — بالظبط زي
    //     Core/IdentityRules.cs مع /js/nuh-id.js. فمستحيل الواجهة تقبل جملة
    //     الخادم بيرفضها، ولا العكس.
    // ============================================================================
    public static class PledgeRules
    {
        // ---------- الجملة المطلوبة ----------
        // ⚠️ عربية دائمًا حتى لو الطالب فاتح الواجهة بالإنجليزية: النصّ الملزِم
        //    هو العربي، والإنجليزي ترجمة للاطّلاع. جملة الإقرار جزء من النصّ
        //    الملزِم مش من واجهة الاستخدام.
        public const string RequiredSentence = "أقر بأنني اطّلعت على البنود وأوافق عليها";

        public const string SentenceError =
            "جملة الإقرار غير مطابقة. اكتبها كما هي: " + RequiredSentence;

        public const string NotAcceptedError =
            "لا يمكن تقديم الطلب قبل الموافقة على التعهدات وسياسة الإسكان.";

        public const string NoTermsError =
            "بنود التعهّد غير متاحة حاليًا. يرجى المحاولة لاحقًا أو مراجعة إدارة الإسكان.";

        // ⚠️ البنود اتغيّرت بين لحظة ما الطالب قراها ولحظة ما بعت. مش خطأ منه،
        //    فالرسالة بتقوله يعيد التحميل — مش «فشل التقديم».
        public const string TermsChangedError =
            "تم تحديث بنود التعهّد أثناء تعبئة الطلب. يرجى إعادة تحميل الصفحة وقراءة البنود من جديد.";

        // ====================================================================
        //  التطبيع قبل المقارنة.
        //
        //  ⚠️ المقارنة الحرفية بتفشل على كتابة سليمة تمامًا:
        //     • التشكيل: «اطّلعت» بالشدّة و«اطلعت» من غيرها — نفس الكلمة،
        //       ولوحة مفاتيح الموبايل بتحطّ الشدّة أو ما بتحطّهاش حسب الجهاز.
        //     • الهمزة: «أقر» و«اقر» — كتير من لوحات المفاتيح مابتديش الألف
        //       بهمزة بسهولة، والطالب مش بيغشّ لما يكتبها من غيرها.
        //     • المسافات: مسافتين بين كلمتين، أو مسافة في الآخر من النسخ.
        //     • علامات الترقيم: نقطة في آخر الجملة.
        //
        //  اللي **مابيتسامحش** فيه: الكلمات نفسها وترتيبها. ودي اللي بتخلّي
        //  الكتابة فعلًا إراديّة.
        // ====================================================================

        // التشكيل والتطويل — بيتشالوا خالص.
        public const string DiacriticsPattern = @"[\u064B-\u0652\u0670\u0640]";

        // صور الألف — بترجع كلها ألف عادية.
        public const string AlefPattern = @"[\u0623\u0625\u0622\u0671]";

        // أي حرف مش حرف أبجدي ولا مسافة (ترقيم، أرقام، رموز) — بيبقى مسافة.
        public const string NonLetterPattern = @"[^\p{L}\s]";

        public const string WhitespacePattern = @"\s+";

        private static readonly Regex DiacriticsRx =
            new(DiacriticsPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AlefRx =
            new(AlefPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NonLetterRx =
            new(NonLetterPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex WhitespaceRx =
            new(WhitespacePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = DiacriticsRx.Replace(raw, string.Empty);
            s = AlefRx.Replace(s, "ا");
            s = s.Replace('ى', 'ي')   // ى → ي
                 .Replace('ة', 'ه');  // ة → ه
            s = NonLetterRx.Replace(s, " ");
            return WhitespaceRx.Replace(s, " ").Trim();
        }

        public static bool SentenceMatches(string? typed)
            => Normalize(typed).Length > 0
            && Normalize(typed) == Normalize(RequiredSentence);

        // ====================================================================
        //  نصّ البنود المجمَّد وبصمته.
        //
        //  ⚠️ الترتيب والترقيم جزء من النصّ عن قصد: «البند ٣» في محضر أو خطاب
        //     لازم يوصّل لنفس السطر. ولو المدير غيّر الترتيب، النصّ بيتغيّر
        //     والبصمة بتتغيّر معاه — وده صح، لأن اللي الطالب قراه بقى غير ده.
        //
        //  ⚠️ النصّ العربي هو المُجمَّد دائمًا مهما كانت لغة الواجهة. لو البصمة
        //     اتحسبت على اللي الطالب شافه، الطالب اللي فاتح بالإنجليزية كان
        //     هيطلعله سجل ببصمة تانية لنفس البنود — فتبقى بصمتين لتعهّد واحد.
        // ====================================================================
        public static string BuildTermsText(IEnumerable<(int DisplayOrder, int Id, string ArText)> terms)
        {
            var sb = new StringBuilder();
            var n = 0;
            foreach (var t in terms.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id))
            {
                var line = WhitespaceRx.Replace(t.ArText ?? string.Empty, " ").Trim();
                if (line.Length == 0) continue;
                n++;
                if (n > 1) sb.Append('\n');
                sb.Append(n).Append(". ").Append(line);
            }
            return sb.ToString();
        }

        public static string ComputeHash(string canonicalText)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText ?? string.Empty)));

        // ⚠️ رقم النسخة مشتقّ من البصمة نفسها مش مكتوب بالإيد. «1.0» الثابتة
        //    القديمة كانت بتتكتب على كل السجلات مهما اتغيّرت البنود، فمكانتش
        //    بتميّز حاجة. الشكل ده بيقول عدد البنود وأول ٨ خانات من البصمة —
        //    قابل للقراءة في الجدول، ومربوط بالنصّ فعلًا.
        public static string VersionOf(string canonicalText, int termCount)
            => "T" + termCount + "-" + ComputeHash(canonicalText)[..8];

        // ====================================================================
        //  نفس القواعد بصيغة جافاسكريبت — تُقدَّم على /js/nuh-pledge.js.
        //
        //  ⚠️ صفحة الإقرار في بوابة الطالب صفحة HTML ثابتة مش بتمرّ على Razor،
        //     فما ينفعش نحقن لها الجملة ولا قاعدة التطبيع. الملف بيتولّد من
        //     الثوابت اللي فوق، فالمتصفح بيفحص بنفس القاعدة اللي الخادم
        //     بيرفض بيها بالحرف.
        //
        //  ⚠️ وفحص المتصفح **تسهيل** مش حماية: بيقول للطالب إن الجملة مظبوطة
        //     وهو بيكتب بدل ما يبعت ويترفض. الحارس الحقيقي في
        //     RegistrationFlowService، وهو اللي بيرفض الطلب.
        // ====================================================================
        private static string? _js;
        public static string ToJavaScript() => _js ??= BuildJavaScript();

        private static string BuildJavaScript() =>
$$"""
// ============================================================================
//  nuh-pledge.js - جملة الإقرار وقاعدة مطابقتها.
//
//  ⚠️ الملف ده **متولَّد** من Core/PledgeRules.cs. ماتعدّلش فيه - أي تعديل
//     هنا بيروح مع أول طلب. القاعدة تتغيّر في ملف C# وبس.
// ============================================================================
var NuhPledge = (function () {
  'use strict';

  var SENTENCE   = {{System.Text.Json.JsonSerializer.Serialize(RequiredSentence)}};
  var DIACRITICS = /{{DiacriticsPattern}}/g;
  var ALEF       = /{{AlefPattern}}/g;
  var NON_LETTER = /{{NonLetterPattern}}/gu;
  var WHITESPACE = /{{WhitespacePattern}}/g;

  var MSG = {
    sentence:     {{System.Text.Json.JsonSerializer.Serialize(SentenceError)}},
    notAccepted:  {{System.Text.Json.JsonSerializer.Serialize(NotAcceptedError)}},
    noTerms:      {{System.Text.Json.JsonSerializer.Serialize(NoTermsError)}},
    termsChanged: {{System.Text.Json.JsonSerializer.Serialize(TermsChangedError)}}
  };

  // نفس ترتيب الخطوات في PledgeRules.Normalize بالحرف.
  function normalize(v) {
    var s = (v == null ? '' : String(v));
    if (!s.trim()) return '';
    s = s.replace(DIACRITICS, '');
    s = s.replace(ALEF, 'ا');
    s = s.replace(/\u0649/g, '\u064A').replace(/\u0629/g, '\u0647');
    s = s.replace(NON_LETTER, ' ');
    return s.replace(WHITESPACE, ' ').trim();
  }

  var TARGET = normalize(SENTENCE);

  function matches(v) { var n = normalize(v); return n.length > 0 && n === TARGET; }

  // نسبة اللي اتكتب صح من أول الجملة - عشان الواجهة تبيّن التقدّم وهو بيكتب
  // بدل ما تقوله «غلط» على جملة لسه ناقصة.
  function progress(v) {
    var n = normalize(v);
    if (!n) return 0;
    var i = 0;
    while (i < n.length && i < TARGET.length && n[i] === TARGET[i]) i++;
    return i / TARGET.length;
  }

  function isPrefix(v) {
    var n = normalize(v);
    return n.length === 0 || TARGET.indexOf(n) === 0;
  }

  return {
    SENTENCE: SENTENCE, MSG: MSG,
    normalize: normalize, matches: matches,
    progress: progress, isPrefix: isPrefix
  };
})();
""";
    }
}
