using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Converters
{
    // AdStatus <-> string: بيخزّن نفس القيم "enabled"/"disabled" (بدون تغيير عمود ولا ترحيل بيانات).
    // مطبّق على عمود nullable — EF بيتولّى الـ null لوحده، والمحوّل بيشوف القيم غير الفاضية بس.
    // مهم: شغّل SQL التطبيع (نزّل '' لـ NULL) قبل النشر عشان فلاتر "بدون حساب" (IS NULL) تبقى مظبوطة.
    public class AdStatusConverter : ValueConverter<AdStatus, string>
    {
        public AdStatusConverter() : base(
            v => v == AdStatus.enabled ? "enabled" : "disabled",
            s => s != null && s.Trim().ToLowerInvariant() == "enabled" ? AdStatus.enabled : AdStatus.disabled)
        {
        }
    }
}
