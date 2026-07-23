using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Converters
{
    // NotificationStatus <-> string: بيخزّن الاسم زي ما هو (pending/read) — بدون تغيير عمود ولا ترحيل.
    // القراءة دفاعية (غير معروف → pending) عشان أي قيمة قديمة متكسرش.
    public class NotificationStatusConverter : ValueConverter<NotificationStatus, string>
    {
        public NotificationStatusConverter() : base(
            v => v.ToString(),
            s => Parse(s))
        {
        }

        private static NotificationStatus Parse(string? s)
            => (s ?? "").Trim().ToLowerInvariant() == "read" ? NotificationStatus.read : NotificationStatus.pending;
    }
}
