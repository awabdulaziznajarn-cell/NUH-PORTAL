using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Converters
{
    // StudentState <-> string: بيخزّن اسم العضو (active/inactive/left) — بدون تغيير عمود ولا ترحيل.
    // القراءة case-insensitive فبتلمّ "Active"/"Left" القديمة تلقائيًا؛ الافتراضي active.
    // (لسه محتاجين SQL تطبيع عشان استعلامات الـ WHERE status='active' تلقّط الصفوف القديمة "Active".)
    public class StudentStateConverter : ValueConverter<StudentState, string>
    {
        public StudentStateConverter() : base(
            v => v.ToString(),
            s => Parse(s))
        {
        }

        private static StudentState Parse(string? s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "left": return StudentState.left;
                case "inactive": return StudentState.inactive;
                default: return StudentState.active;
            }
        }
    }
}
