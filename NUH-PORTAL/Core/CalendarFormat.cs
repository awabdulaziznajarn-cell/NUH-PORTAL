using System.Globalization;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  شكل التاريخ في المخرجات المبنيّة على الخادم (Excel / PDF / الطباعة).
    //  التعريف الوحيد، والمقابل الحرفي لـ wwwroot/js/date-format.js في الواجهة.
    //
    //  ⚠️ ليه الملف ده موجود:
    //     المستخدم بيبدّل التقويم لهجري من التقويم في الشاشة، فالجداول والحقول
    //     بتتقلب هجري. وبعدين يضغط «Excel» أو «PDF» فيطلع له الملف **ميلادي** —
    //     لأن الملف بيتبني على الخادم واللي ما بيشوفش اختيار المتصفح أصلًا.
    //     والفرق ده أسوأ من عدم دعم الهجري من أساسه: الموظف بيراجع ورقة مطبوعة
    //     على شاشة، والتواريخ في الاتنين مختلفة، فيفتكر إن البيانات نفسها غلط.
    //
    //  ⚠️ التخزين ميلادي دايمًا — ده عرض بحت. قرار مقصود: أي تحويل عند التخزين
    //     بيخلّي نفس الصفّ يتقرا بتاريخين حسب مين فاتحه، وبيكسر كل مقارنة
    //     وفلترة وتقرير.
    //
    //  ⚠️ UmAlQuraCalendar لا حساب بالإيد: ده نفس التقويم الرسمي المعتمد في
    //     السعودية، ومتوافق مع اللي المتصفح بيحسبه (islamic-umalqura) — فالورقة
    //     والشاشة بيقولوا نفس اليوم. أي جدول محسوب بالإيد كان هيفرق يوم في
    //     شهور معيّنة، والفرق ده ما بيبانش غير لما حد يقارن.
    // ============================================================================
    public static class CalendarFormat
    {
        private static readonly UmAlQuraCalendar Hijri = new();

        public static readonly string[] HijriMonthsAr =
        {
            "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
            "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"
        };

        // "hijri" من الواجهة، وأي حاجة تانية معناها ميلادي — منع افتراضي:
        // قيمة غير متوقّعة تطلع ميلادي (وهو التخزين) لا تقويمًا مش مقصود.
        public static bool IsHijri(string? calendar) =>
            string.Equals(calendar?.Trim(), "hijri", StringComparison.OrdinalIgnoreCase);

        // 18/08/2026  أو  05/03/1448 هـ
        public static string Date(DateTime d, string? calendar = null)
        {
            if (!IsHijri(calendar)) return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            try
            {
                return $"{Hijri.GetDayOfMonth(d):00}/{Hijri.GetMonth(d):00}/{Hijri.GetYear(d)} هـ";
            }
            catch (ArgumentOutOfRangeException)
            {
                // ⚠️ UmAlQura بيغطّي ١٩٠٠–٢٠٧٧ تقريبًا. تاريخ بره المدى بيرمي،
                //    والرمي هنا معناه تقرير فاضي بدل تاريخ واحد غلط — فبنرجع
                //    الميلادي بدل ما نكسر التصدير كله.
                return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
        }

        // 18/08/2026 22:44  أو  05/03/1448 هـ 22:44
        public static string DateTimeText(DateTime d, string? calendar = null) =>
            Date(d, calendar) + " " + d.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
