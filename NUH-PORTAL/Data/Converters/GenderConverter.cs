using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Converters
{
    // يخزّن Gender في قاعدة البيانات كـ "male"/"female" (نفس القيم الحالية — صفر ترحيل)
    public class GenderConverter : ValueConverter<Gender, string>
    {
        public GenderConverter() : base(
            g => g == Gender.Male ? "male" : "female",
            s => s == "male" ? Gender.Male : Gender.Female)
        { }
    }
}
