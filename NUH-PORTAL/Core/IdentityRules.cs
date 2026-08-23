using System.Text.RegularExpressions;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  قواعد أرقام الهوية والجوال — التعريف الوحيد في النظام.
    //
    //  ⚠️ كانت هذه القواعد مكتوبة في كل خدمة على حدة وتفارقت فعلًا:
    //
    //     • الرقم الجامعي: StudentService شُدِّد إلى ^4\d{8}$ بينما بقي
    //       BulkRegistrationService على ^\d{9,10}$ — فرقم مثل 912345678 يمرّ
    //       من رفع الإكسل ويُحفظ، ثم يرفضه كل نموذج فردي، فلا يمكن تعديل
    //       السجل ولا حفظه أبدًا.
    //
    //     • رقم الجوال: خمس نسخ بعائلتين متعارضتين — NormalizePhone لا تعرف
    //       بادئة 00966 وتُرجع النص كما هو عند الفشل، بينما NormalizeMobile
    //       تجرّدها وتُرجع null. فمن يكتب 00966501234567 يُحفظ رقمه بصيغة،
    //       ويُبحث عنه بصيغة أخرى، فلا يستطيع التحقق ولا متابعة طلبه.
    // ============================================================================
    public static class IdentityRules
    {
        // ---------- الرقم الجامعي ----------
        // جامعة نجران: تسعة أرقام بالضبط تبدأ بـ 4.
        public const string StudentIdPattern = @"^4\d{8}$";

        private static readonly Regex StudentIdRx =
            new(StudentIdPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsValidStudentId(string? studentId)
            => !string.IsNullOrWhiteSpace(studentId) && StudentIdRx.IsMatch(studentId.Trim());

        public const string StudentIdError = "الرقم الجامعي: يجب أن يبدأ بالرقم 4 ويتكون من 9 أرقام";

        // ---------- رقم الهوية / الإقامة ----------
        // ⚠️ القاعدة دي كانت مكتوبة بتلات صور، واتنين منها **أضعف** من التالت:
        //
        //     FacultyHousingService  ١٠ أرقام + يبدأ بـ ١ أو ٢ + يرفض أرقام الجوال
        //     StudentService         ^\d{10}$  — أي عشرة أرقام
        //     الواجهة (٧ مواضع)      ^\d{10}$  — أي عشرة أرقام
        //
        //   يعني رقم جوال مكتوب في خانة الهوية بيترفض في شاشة سكن أعضاء هيئة
        //   التدريس وبيتقبل في تسجيل الطالب. وده مش فرض نظري: الفحص الأقوى
        //   اتكتب أصلًا لأن الدومين فيه ١٣٥ حساب مكتوب في employeeID بتاعهم
        //   رقم الجوال بدل الهوية — بيانات موروثة من إدخال بلا فحص.
        //
        //   القاعدة الأقوى هي الصح، وهي اللي بقت للكل.
        //
        //  ⚠️ لكن شرط «تبدأ بـ ١ أو ٢» اتشال بعد كده عن قصد: الطالب الوافد
        //     بيدخل بـ **رقم الحدود** لحد ما تطلع إقامته، ورقم الحدود مابيبدأش
        //     بـ ١ ولا ٢. فالشرط ده كان هيقفل التسجيل في وشّه — ومنع تسجيل
        //     طالب حقيقي أسوأ بكتير من قبول رقم بصيغة غير متوقّعة.
        //
        //     اللي فضل هو الجزء اللي كان بيحلّ مشكلة حقيقية: رفض أرقام الجوال
        //     المكتوبة في خانة الهوية (اللي عملت ١٣٥ حساب في الدومين بـ employeeID
        //     غلط). ده الفحص اللي ليه سبب في البيانات، وشرط البداية مكانش.
        // عشرة أرقام — هوية وطنية أو إقامة أو رقم حدود.
        public const string NationalIdPattern = @"^\d{10}$";

        private static readonly Regex NationalIdRx =
            new(NationalIdPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsValidNationalId(string? nationalId)
            => !string.IsNullOrWhiteSpace(nationalId) && NationalIdRx.IsMatch(nationalId.Trim());

        public const string NationalIdError =
            "رقم الهوية يجب أن يتكوّن من 10 أرقام";

        // ⚠️ رسالة منفصلة لمّا المُدخَل يبان رقم جوال: «١٠ أرقام تبدأ بـ ١ أو ٢»
        //    ما بتقولش للموظف إيه اللي غلط في اللي كتبه، ودي بتقوله.
        public const string NationalIdIsMobileError =
            "القيمة المُدخلة رقم جوال وليست رقم هوية.";

        // ⚠️ نمط لا سلسلة StartsWith عشان الواجهة تاخده زي ما هو من ToJavaScript.
        public const string MobileLikePattern = @"^(?:05|9665)";

        private static readonly Regex MobileLikeRx =
            new(MobileLikePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool LooksLikeMobile(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return MobileLikeRx.IsMatch(new string(raw.Where(char.IsDigit).ToArray()));
        }

        // ---------- رقم الجوال ----------
        // الصيغة المخزَّنة في النظام وفي الدليل النشط: 9665XXXXXXXX.
        public const string StoredMobilePattern = @"^9665\d{8}$";

        // يقبل 05XXXXXXXX و 5XXXXXXXX و 9665XXXXXXXX و 009665XXXXXXXX،
        // ويُرجع null إن لم يكن رقمًا سعوديًّا صالحًا للجوال.
        public static string? NormalizeMobile(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var d = new string(raw.Where(char.IsDigit).ToArray());
            if (d.Length == 0) return null;

            if (d.StartsWith("00966", StringComparison.Ordinal)) d = d[2..];
            if (d.StartsWith("966", StringComparison.Ordinal)) d = d[3..];
            if (d.StartsWith("0", StringComparison.Ordinal)) d = d[1..];

            return d.Length == 9 && d[0] == '5' ? "966" + d : null;
        }

        // نسخة لا تُرجع null: تُستخدم حيث يقارَن رقمان أو يُبحث بالقيمتين معًا.
        // ⚠️ لا تُرجع null عمدًا: لو أرجعناها، قارن الكود رقمين غير صالحين
        //    فوجد null == null صحيحًا واعتبرهما نفس الشخص.
        public static string NormalizeMobileOrDigits(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            return NormalizeMobile(raw) ?? new string(raw.Where(char.IsDigit).ToArray());
        }

        // هل الرقمان لنفس الجوال؟ يقارن بعد التوحيد لا كنصّين.
        public static bool SameMobile(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return NormalizeMobileOrDigits(a) == NormalizeMobileOrDigits(b);
        }

        public const string MobileError =
            "رقم الجوال يجب أن يبدأ بالرقم 5 وأن يتكوّن من 9 أرقام (مثال: 512345678)";

        // ====================================================================
        //  نفس القواعد بصيغة جافاسكريبت — تُقدَّم على /js/nuh-id.js.
        //
        //  ⚠️ الواجهة محتاجة تفحص وقت الكتابة بلا نداء للخادم، وبوابة الطالب
        //     صفحات HTML ثابتة مش بتعدّي على Razor فما ينفعش نحقن فيها زي __WF.
        //     الحلّ إن الملف نفسه يتولّد من هنا: الأنماط والرسائل بتتقرا من
        //     الثوابت اللي فوق، فمستحيل الواجهة تفحص بقاعدة والخادم بقاعدة تانية.
        //
        //  ⚠️ كان في ملف ثابت wwwroot/js/identity-rules.js فيه نسخة مكتوبة
        //     بالإيد. اتشال، لأن «نسخة متطابقة بالمراجعة» بتفضل متطابقة لحد أول
        //     تعديل مستعجل.
        //
        //  ⚠️ الأنماط بتتكتب بين / / لا كنصّ في new RegExp: النصّ كان هيحتاج
        //     تهريب مزدوج للشرطات المائلة العكسية، وده بالظبط مكان الغلط الصامت.
        // ====================================================================
        // ⚠️ النصّ ثابت طول عمر العملية — بيتبني مرة واحدة بدل ما يتصرف
        //    JsonSerializer وبناء نصّ مع كل طلب لملف بيتطلب من كل صفحة.
        private static string? _js;

        public static string ToJavaScript() => _js ??= BuildJavaScript();

        private static string BuildJavaScript() =>
$$"""
// ============================================================================
//  nuh-id.js - قواعد الرقم الجامعي والهوية والجوال.
//
//  ⚠️ الملف ده **متولَّد** من Core/IdentityRules.cs. ماتعدّلش فيه - أي تعديل
//     هنا بيروح مع أول طلب. القاعدة تتغيّر في ملف C# وبس.
// ============================================================================
var NuhId = (function () {
  'use strict';

  var STUDENT_ID    = /{{StudentIdPattern}}/;
  var NATIONAL_ID   = /{{NationalIdPattern}}/;
  var STORED_MOBILE = /{{StoredMobilePattern}}/;
  var MOBILE_LIKE   = /{{MobileLikePattern}}/;

  var MSG = {
    studentId:        {{System.Text.Json.JsonSerializer.Serialize(StudentIdError)}},
    nationalId:       {{System.Text.Json.JsonSerializer.Serialize(NationalIdError)}},
    nationalIdMobile: {{System.Text.Json.JsonSerializer.Serialize(NationalIdIsMobileError)}},
    mobile:           {{System.Text.Json.JsonSerializer.Serialize(MobileError)}}
  };

  function digits(v) { return String(v == null ? '' : v).replace(/\D/g, ''); }
  function trimmed(v) { return String(v == null ? '' : v).trim(); }

  function isStudentId(v)  { return STUDENT_ID.test(trimmed(v)); }
  function isNationalId(v) { return NATIONAL_ID.test(trimmed(v)); }
  function isStoredMobile(v) { return STORED_MOBILE.test(trimmed(v)); }

  // ⚠️ رقم الهوية عشرة أرقام بأي بداية (الوافد بيدخل برقم الحدود لحد ما تطلع
  //    إقامته)، فالفحص الوحيد الباقي هو إن المكتوب مش رقم جوال بالغلط.
  function looksLikeMobile(v) { return MOBILE_LIKE.test(digits(v)); }

  return {
    STUDENT_ID: STUDENT_ID, NATIONAL_ID: NATIONAL_ID,
    STORED_MOBILE: STORED_MOBILE, MOBILE_LIKE: MOBILE_LIKE,
    MSG: MSG,
    isStudentId: isStudentId, isNationalId: isNationalId,
    isStoredMobile: isStoredMobile, looksLikeMobile: looksLikeMobile,
    digits: digits
  };
})();
""";
    }
}
