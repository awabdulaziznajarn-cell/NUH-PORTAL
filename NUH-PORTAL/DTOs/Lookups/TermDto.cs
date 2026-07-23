namespace NUH_PORTAL.DTOs.Lookups
{
    public class TermDto
    {
        public int Id { get; set; }
        public string ArText { get; set; } = string.Empty;
        public string EnText { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class TermSaveDto
    {
        public string ArText { get; set; } = string.Empty;
        public string EnText { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
