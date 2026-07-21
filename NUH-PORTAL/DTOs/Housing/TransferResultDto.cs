namespace NUH_PORTAL.DTOs.Housing
{
    public class TransferResultDto
    {
        public string? Message { get; set; }
        public int TransferId { get; set; }
        public string? OldLocation { get; set; }
        public string? NewLocation { get; set; }
    }
}
