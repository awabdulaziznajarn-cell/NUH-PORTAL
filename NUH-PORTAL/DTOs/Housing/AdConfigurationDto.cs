namespace NUH_PORTAL.DTOs.Housing
{
    // سطر إعدادات AD (مرآة الـ entity من غير الـ navigation)
    public class AdConfigurationDto
    {
        public int Id { get; set; }
        public string? ConfigKey { get; set; }
        public string? ConfigValue { get; set; }
        public string? Description { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
