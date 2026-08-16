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
    }
}
