using System.Text.RegularExpressions;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Services
{
    public static class GenderHelper
    {
        // يحوّل أي مدخل نصّي (ذكر/male/m/أنثى/female/f) إلى enum؛ null لو غير صالح
        public static Gender? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim().ToLowerInvariant() switch
            {
                "ذكر" or "male" or "m" => Gender.Male,
                "أنثى" or "انثى" or "female" or "f" => Gender.Female,
                _ => null
            };
        }

        // يحوّل الـ enum لنص التخزين/العرض "male"/"female"
        public static string? ToStr(Gender? g) => g switch
        {
            Gender.Male => "male",
            Gender.Female => "female",
            _ => null
        };

        private static readonly HashSet<string> ValidInputs = new(StringComparer.OrdinalIgnoreCase)
        {
            "ذكر", "male", "m",
            "أنثى", "انثى", "female", "f"
        };

        public static bool IsValid(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && ValidInputs.Contains(value.Trim());
        }

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            return trimmed.ToLowerInvariant() switch
            {
                "ذكر" or "male" or "m" => "male",
                "أنثى" or "انثى" or "female" or "f" => "female",
                _ => value
            };
        }

        public static string NormalizeSafely(string? value)
        {
            var normalized = Normalize(value);
            if (normalized != "male" && normalized != "female")
                return string.Empty;
            return normalized;
        }

        public static (bool IsValid, string? Normalized) ValidateAndNormalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (false, null);
            var trimmed = value.Trim();
            var normalized = trimmed.ToLowerInvariant() switch
            {
                "ذكر" or "male" or "m" => "male",
                "أنثى" or "انثى" or "female" or "f" => "female",
                _ => null
            };
            return (normalized != null, normalized);
        }
    }
}
