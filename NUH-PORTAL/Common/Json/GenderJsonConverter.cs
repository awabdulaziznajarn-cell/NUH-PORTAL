using System.Text.Json;
using System.Text.Json.Serialization;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Services;

namespace NUH_PORTAL.Common.Json
{
    // يحافظ على شكل الـ gender في الـ JSON كـ "male"/"female" (زي ما الـ JS والفورم متوقّعين) —
    // فتحويل الحقل لـ enum مبيكسرش أي حاجة في الواجهة.
    public class GenderJsonConverter : JsonConverter<Gender?>
    {
        public override Gender? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            var s = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            return GenderHelper.Parse(s); // بيرجّع null لو القيمة مش صالحة
        }

        public override void Write(Utf8JsonWriter writer, Gender? value, JsonSerializerOptions options)
        {
            if (value is null) { writer.WriteNullValue(); return; }
            writer.WriteStringValue(value == Gender.Male ? "male" : "female");
        }
    }
}
