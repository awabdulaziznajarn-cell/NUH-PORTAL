using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  توثيق التعهّد - التعريف الوحيد في النظام.
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
    //     وكمان: صفحة الإقرار مكانتش بتنادي الـ API أصلًا - كانت بتكتب
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
    //         بحرف فعل إرادي. والتحقّق منها **على الخادم** - لأن أي فحص في
    //         المتصفح بيتشال من أدوات المطوّر في ثانية.
    //
    //  ⚠️ والقاعدة مكتوبة هنا مرة واحدة: نصّ الجملة والتطبيع والبصمة. الواجهة
    //     بتاخد نفس القواعد متولّدة منها على /js/nuh-pledge.js - بالظبط زي
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
        //    فالرسالة بتقوله يعيد التحميل - مش «فشل التقديم».
        public const string TermsChangedError =
            "تم تحديث بنود التعهّد أثناء تعبئة الطلب. يرجى إعادة تحميل الصفحة وقراءة البنود من جديد.";

        // ====================================================================
        //  التطبيع قبل المقارنة.
        //
        //  ⚠️ المقارنة الحرفية بتفشل على كتابة سليمة تمامًا:
        //     • التشكيل: «اطّلعت» بالشدّة و«اطلعت» من غيرها - نفس الكلمة،
        //       ولوحة مفاتيح الموبايل بتحطّ الشدّة أو ما بتحطّهاش حسب الجهاز.
        //     • الهمزة: «أقر» و«اقر» - كتير من لوحات المفاتيح مابتديش الألف
        //       بهمزة بسهولة، والطالب مش بيغشّ لما يكتبها من غيرها.
        //     • المسافات: مسافتين بين كلمتين، أو مسافة في الآخر من النسخ.
        //     • علامات الترقيم: نقطة في آخر الجملة.
        //
        //  اللي **مابيتسامحش** فيه: الكلمات نفسها وترتيبها. ودي اللي بتخلّي
        //  الكتابة فعلًا إراديّة.
        // ====================================================================

        // التشكيل والتطويل - بيتشالوا خالص.
        public const string DiacriticsPattern = @"[\u064B-\u0652\u0670\u0640]";

        // صور الألف - بترجع كلها ألف عادية.
        public const string AlefPattern = @"[\u0623\u0625\u0622\u0671]";

        // أي حرف مش حرف أبجدي ولا مسافة (ترقيم، أرقام، رموز) - بيبقى مسافة.
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
        //     والبصمة بتتغيّر معاه - وده صح، لأن اللي الطالب قراه بقى غير ده.
        //
        //  ⚠️ النصّ العربي هو المُجمَّد دائمًا مهما كانت لغة الواجهة. لو البصمة
        //     اتحسبت على اللي الطالب شافه، الطالب اللي فاتح بالإنجليزية كان
        //     هيطلعله سجل ببصمة تانية لنفس البنود - فتبقى بصمتين لتعهّد واحد.
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
        //    بتميّز حاجة. الشكل ده بيقول عدد البنود وأول ٨ خانات من البصمة -
        //    قابل للقراءة في الجدول، ومربوط بالنصّ فعلًا.
        public static string VersionOf(string canonicalText, int termCount)
            => "T" + termCount + "-" + ComputeHash(canonicalText)[..8];

        // ====================================================================
        //  رمز التحقّق المطبوع على الوثيقة.
        //
        //  المشكلة: بصمة البنود مش ختمًا للورقة - هي SHA-256 لنصّ البنود وحده،
        //  فكل طالب وقّع على نفس النسخة بصمته **نفس البصمة**. يعني اللي يزوّر
        //  ورقة مش محتاج يخمّنها، بينقلها من ورقة أي طالب تاني. وجملة التذييل
        //  كانت بتقول «للتحقّق طابِق البصمة مع سجل الطلب» - وde تحقّق مش واقع:
        //  المطابقة بتقول «دي نسخة البنود الفلانية» لا «دي ورقة هذا الطالب».
        //
        //  الحلّ: رمز خاص بكل وثيقة، محسوب بـ HMAC-SHA256 بمفتاح **ما يخرجش
        //  من الخادم**. الفرق عن SHA العادي إن SHA دالة عامة: اللي يعرف
        //  المدخلات (وهي كلها مطبوعة على الورقة) يحسب النتيجة بنفسه. بلا
        //  المفتاح مفيش طريق لتوليد رمز صحيح لورقة ملفّقة.
        //
        //  ⚠️ رقم الطلب جوّه الرمز عن قصد: من غيره التحقّق لازم يعيد الحساب
        //     على كل السجلات لحد ما يلاقي واحدة تطابق. وهو مش سرّ أصلًا -
        //     رقم الطلب مطبوع في ترويسة نفس الورقة.
        //
        //  ⚠️ ورقم الجيل في أول خانة: لو المفتاح اتسرّب أو اتبدّل، الأوراق
        //     المطبوعة قبل التبديل لازم تفضل قابلة للتحقّق. من غير رقم الجيل
        //     التدوير معناه إبطال كل ورقة اتطبعت قبله.
        //
        //  ⚠️ وده **ما بيوقّعش نصّ البنود**: بيربط الورقة بسجلها. الشاهد على
        //     النصّ يفضل TermsText المجمَّد وبصمته - وهما المرجع عند الخلاف.
        // ====================================================================

        // ⚠️ أبجدية بلا I و L و O و U: الرمز بيتقرا من ورقة مطبوعة وبيتكتب
        //    بالإيد، و«O/0» و«I/1» بيتبدّلوا كل يوم. (نفس منطق Crockford Base32.)
        private const string CodeAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        // ⚠️ البادئة مش جزء من التوقيع: النصّ المُوقَّع عليه (Canonical) مافيهوش
        //    بادئة أصلًا، فتغييرها مابيبطّلش أي رمز اتولّد قبل كده - والبادئة
        //    القديمة لسه مقبولة عند القراءة (شوف NormalizeDocCode).
        //    وظيفتها الوحيدة إن اللي بيلاقي الرمز على ورقة يعرف ده بتاع إيه.
        public const string DocCodePrefix = "NUH";

        // بادئات قديمة تُقبل عند القراءة ولا تُطبع. الأحدث أولًا - «NU» بادئة
        // لـ «NUH»، فلو اتفحصت الأول كانت هتسيب H تدخل في الرمز.
        private static readonly string[] AcceptedPrefixes = { "NUH", "NU" };

        // ٤ خانات لرقم الطلب = 32⁴ طلب. فوق كده مفيش رمز - والوثيقة بتتطبع
        // بلا رمز بدل ما تطلع برمز غلط.
        private const int RequestIdChars = 4;
        private const int MacChars = 7;                                  // ٣٥ بت
        private const long MaxRequestId = 1L << (5 * RequestIdChars);    // 1,048,576

        private static string Base32(long value, int chars)
        {
            var buf = new char[chars];
            for (var i = chars - 1; i >= 0; i--)
            {
                buf[i] = CodeAlphabet[(int)(value & 31)];
                value >>= 5;
            }
            return new string(buf);
        }

        private static string Base32(byte[] bytes, int chars)
        {
            var sb = new StringBuilder(chars);
            int acc = 0, bits = 0;
            foreach (var b in bytes)
            {
                acc = (acc << 8) | b;
                bits += 8;
                while (bits >= 5 && sb.Length < chars)
                {
                    bits -= 5;
                    sb.Append(CodeAlphabet[(acc >> bits) & 31]);
                }
                if (sb.Length >= chars) break;
            }
            return sb.ToString();
        }

        // ⚠️ النصّ المُوقَّع عليه: رقم الجيل ورقم الطلب وبصمة البنود ولحظة
        //    التوقيع. أي تعديل في أي واحد منهم بيغيّر الرمز - فورقة اتعدّل
        //    فيها تاريخ التوقيع بتسقط عند التحقّق.
        private static string Canonical(int keyGeneration, int requestId, string? termsHash, DateTime acceptedAt)
            => $"v1|{keyGeneration}|{requestId}|{(termsHash ?? string.Empty).ToUpperInvariant()}|{acceptedAt.Ticks}";

        private static string Mac(string canonical, string signingKey)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
            return Base32(h.ComputeHash(Encoding.UTF8.GetBytes(canonical)), MacChars);
        }

        // بترجّع الرمز بصيغته المعروضة (NU-XXXX-XXXX-XXXX)، أو null لو ناقص
        // شرط من شروطه. null معناها «اطبع الوثيقة بلا رمز» لا «اطبع رمزًا فاضيًا».
        public static string? BuildDocCode(
            int requestId, string? termsHash, DateTime acceptedAt, string? signingKey, int keyGeneration = 1)
        {
            if (string.IsNullOrWhiteSpace(signingKey)) return null;
            if (string.IsNullOrWhiteSpace(termsHash)) return null;
            if (requestId <= 0 || requestId >= MaxRequestId) return null;
            if (keyGeneration < 0 || keyGeneration > 31) return null;

            var payload = Base32(keyGeneration, 1)
                        + Base32(requestId, RequestIdChars)
                        + Mac(Canonical(keyGeneration, requestId, termsHash, acceptedAt), signingKey);

            return FormatDocCode(payload);
        }

        // ⚠️ مجموعات رباعية: الرمز المتّصل بيتقرا غلط من الورق.
        private static string FormatDocCode(string payload)
        {
            var sb = new StringBuilder(DocCodePrefix);
            for (var i = 0; i < payload.Length; i++)
            {
                if (i % 4 == 0) sb.Append('-');
                sb.Append(payload[i]);
            }
            return sb.ToString();
        }

        // ⚠️ التطبيع قبل القراءة: اللي بيكتب الرمز من الورقة بيحطّ مسافات بدل
        //    الشرطات، وبيكتب حروفًا صغيرة، وساعات بيكتب O مكان 0 و I مكان 1.
        //    الحروف دي مش في الأبجدية أصلًا، فبنردّها لأقرب رقم بدل ما نرفض
        //    قراءة صحيحة كتبها إنسان.
        //
        // ⚠️ وبنشيل البادئة NU بعد التطبيع: حرف N جوّه الأبجدية فبيفضل، و U
        //    بيتحوّل لـ V - فلو سِبناهم الرمز الكامل ما كانش هيتقرا أبدًا.
        public static string NormalizeDocCode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var s = raw.Trim();
            foreach (var p in AcceptedPrefixes)
            {
                if (!s.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
                s = s[p.Length..];
                break;
            }

            var sb = new StringBuilder();
            foreach (var ch in s.ToUpperInvariant())
            {
                var c = ch switch { 'O' => '0', 'I' => '1', 'L' => '1', 'U' => 'V', _ => ch };
                if (CodeAlphabet.IndexOf(c) >= 0) sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool TryParseDocCode(string? raw, out int keyGeneration, out int requestId, out string mac)
        {
            keyGeneration = 0; requestId = 0; mac = string.Empty;

            var s = NormalizeDocCode(raw);
            if (s.Length != 1 + RequestIdChars + MacChars) return false;

            keyGeneration = CodeAlphabet.IndexOf(s[0]);
            if (keyGeneration < 0) return false;

            long id = 0;
            for (var i = 1; i <= RequestIdChars; i++)
            {
                var v = CodeAlphabet.IndexOf(s[i]);
                if (v < 0) return false;
                id = (id << 5) | (uint)v;
            }
            if (id <= 0 || id >= MaxRequestId) return false;
            requestId = (int)id;

            mac = s[(1 + RequestIdChars)..];
            return true;
        }

        // ⚠️ المقارنة بزمن ثابت: المقارنة العادية بتقف عند أول حرف مختلف،
        //    وفرق الزمن ده بيسمح بتخمين الرمز حرفًا حرفًا لو حد قاس الردود.
        public static bool DocCodeMacMatches(
            string mac, int keyGeneration, int requestId, string? termsHash, DateTime acceptedAt, string? signingKey)
        {
            if (string.IsNullOrEmpty(mac) || string.IsNullOrWhiteSpace(signingKey)) return false;
            var expected = Mac(Canonical(keyGeneration, requestId, termsHash, acceptedAt), signingKey);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(mac), Encoding.UTF8.GetBytes(expected));
        }

        // مسار التحقّق - نصّ واحد للورقة وللـ QR ولصفحة التحقّق نفسها.
        public const string VerifyPath = "/Verify";
        public static string VerifyUrlFor(string code) => VerifyPath + "?c=" + Uri.EscapeDataString(code);

        // ====================================================================
        //  نفس القواعد بصيغة جافاسكريبت - تُقدَّم على /js/nuh-pledge.js.
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

  // ==========================================================================
  //  رمز التحقّق المطبوع على الوثيقة - نفس أبجدية Core/PledgeRules بالحرف.
  //
  //  ⚠️ الأبجدية والبادئة والطول متولّدين من C# لا مكتوبين هنا: الشاشة
  //     بتقنّع الحقل وبتقرّر «ده رمز ولا رقم»، والخادم بيقرا نفس النصّ.
  //     حرف واحد مختلف بين الاتنين معناه رمز يتقبل في الشاشة ويترفض في
  //     الخادم - أو العكس، وهو الأسوأ.
  //
  //  ⚠️ و looksLike بتقول «شكله رمز» لا «رمز صحيح»: أول حرف أبجدي بيكفي
  //     عشان الشاشة تقلب الخانة وتحطّ البادئة وهو لسه بيكتب. الصحّة
  //     الحقيقية عند الخادم، والشاشة بتفحص الطول بس قبل ما تبعت.
  // ==========================================================================
  var CODE = {
    PREFIX:   {{System.Text.Json.JsonSerializer.Serialize(DocCodePrefix)}},
    ALPHABET: {{System.Text.Json.JsonSerializer.Serialize(CodeAlphabet)}},
    LENGTH:   {{RequestIdChars + MacChars + 1}},
    GROUP:    4
  };

  // نفس بدائل PledgeRules.NormalizeDocCode: اللي بيكتب من ورقة بيبدّل
  // O بـ 0 و I بـ 1 على طول، فبنردّهم بدل ما نرفض قراءة صحيحة.
  var CODE_SWAP = { 'O': '0', 'I': '1', 'L': '1', 'U': 'V' };

  function looksLikeCode(v) { return /[A-Za-z]/.test(String(v == null ? '' : v)); }

  function codeNormalize(v) {
    var s = String(v == null ? '' : v).trim().toUpperCase();
    if (s.indexOf(CODE.PREFIX) === 0) s = s.slice(CODE.PREFIX.length);
    var out = '';
    for (var i = 0; i < s.length && out.length < CODE.LENGTH; i++) {
      var ch = CODE_SWAP[s[i]] || s[i];
      if (CODE.ALPHABET.indexOf(ch) >= 0) out += ch;
    }
    return out;
  }

  function codeFormat(v) {
    var s = codeNormalize(v), out = '';
    for (var i = 0; i < s.length; i++) {
      if (i > 0 && i % CODE.GROUP === 0) out += '-';
      out += s[i];
    }
    return out;
  }

  function codeComplete(v) { return codeNormalize(v).length === CODE.LENGTH; }

  return {
    SENTENCE: SENTENCE, MSG: MSG,
    normalize: normalize, matches: matches,
    progress: progress, isPrefix: isPrefix,
    CODE: CODE, looksLikeCode: looksLikeCode,
    codeNormalize: codeNormalize, codeFormat: codeFormat, codeComplete: codeComplete
  };
})();
""";
    }
}
