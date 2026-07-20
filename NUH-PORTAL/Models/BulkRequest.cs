namespace NUH_PORTAL.Models
{
    public class BulkRequest
    {
        public int Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public int ValidCount { get; set; }
        public int ErrorCount { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "pending";

        public List<BulkRequestStudent>? Students { get; set; }
    }
}
