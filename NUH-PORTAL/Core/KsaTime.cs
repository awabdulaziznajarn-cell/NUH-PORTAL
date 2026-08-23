namespace NUH_PORTAL.Core
{
    // ========================================================================
    //  ⚠️ فرق التوقيت السعودي في مكان واحد.
    //
    //     التواريخ تُخزَّن في قاعدة البيانات بتوقيت UTC، والمستخدم يختار
    //     تاريخًا بتوقيت السعودية. بلا الإزاحة يُحسب طلبٌ قُدِّم الواحدة صباحًا
    //     على اليوم السابق، فيختفي من نتيجة «من 17/08».
    //
    //     كانت الإزاحة مكتوبة داخل AuditLogQueryService.GetTodayStatsAsync
    //     وحدها، بينما فلتر «من / إلى» في الشاشة نفسها يقارن بلا إزاحة أصلًا -
    //     أي أن الشاشة الواحدة كانت تحسب اليوم بطريقتين.
    //
    //     السعودية على +3 ثابتة بلا توقيت صيفي، فالإزاحة رقم لا منطقة زمنية:
    //     TimeZoneInfo ليس له معرّف واحد يعمل على ويندوز ولينكس معًا
    //     ("Arab Standard Time" مقابل "Asia/Riyadh").
    // ========================================================================
    public static class KsaTime
    {
        public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

        // اللحظة الحالية بتوقيت السعودية
        public static DateTime Now => DateTime.UtcNow + Offset;

        // تاريخ اليوم بتوقيت السعودية
        public static DateTime Today => Now.Date;

        // بداية اليوم المحلي معبَّرًا عنها بتوقيت UTC - للمقارنة بـ >=
        public static DateTime StartOfDayUtc(DateTime localDate) => localDate.Date - Offset;

        // بداية اليوم التالي معبَّرًا عنها بتوقيت UTC - للمقارنة بـ <
        // ⚠️ عمدًا بداية اليوم التالي لا نهاية اليوم المختار: المقارنة بـ <=
        //    على منتصف الليل تُسقط اليوم المختار كله عدا اللحظة الأولى منه.
        public static DateTime EndOfDayUtc(DateTime localDate) => localDate.Date.AddDays(1) - Offset;
    }
}
