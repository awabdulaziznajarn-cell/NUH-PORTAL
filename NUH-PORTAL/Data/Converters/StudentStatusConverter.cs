using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Converters
{
    // StudentStatus <-> string: بيخزّن اسم العضو زي ما هو
    // (active/dismissed/graduated/transferred/left_housing) — بدون تغيير عمود ولا ترحيل.
    // مطبّق على عمود nullable — EF بيتولّى الـ null، والقراءة دفاعية (غير معروف → active).
    public class StudentStatusConverter : ValueConverter<StudentStatus, string>
    {
        public StudentStatusConverter() : base(
            v => v.ToString(),
            s => Parse(s))
        {
        }

        private static StudentStatus Parse(string? s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "dismissed": return StudentStatus.dismissed;
                case "graduated": return StudentStatus.graduated;
                case "transferred": return StudentStatus.transferred;
                case "left_housing": return StudentStatus.left_housing;
                default: return StudentStatus.active;
            }
        }
    }
}
