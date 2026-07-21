using NUH_PORTAL.DTOs.Students;

namespace NUH_PORTAL.DTOs.Housing
{
    public class HousingAccountDetailsDto
    {
        public HousingStudentDto? Student { get; set; }
        public AdAccountDetailsDto? AdDetails { get; set; }
        public List<LifecycleLogDto> LifecycleLogs { get; set; } = new();
    }
}
