using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Housing
{
    public class ToggleAccountResultDto
    {
        public string? Message { get; set; }
        public AdStatus? ad_status { get; set; }
    }
}
