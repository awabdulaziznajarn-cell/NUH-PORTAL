using NUH_PORTAL.DTOs.Students;

namespace NUH_PORTAL.DTOs.Requests
{
    // نفس شكل رد GET /api/Requests/{id} القديم بالظبط (بأسماء المراجعين)
    public class RequestDetailsDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? RequestType { get; set; }
        public int StudentId { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? HousingReviewedAt { get; set; }
        public int? HousingReviewedBy { get; set; }
        public string? HousingNotes { get; set; }
        public DateTime? CyberReviewedAt { get; set; }
        public int? CyberReviewedBy { get; set; }
        public string? CyberNotes { get; set; }
        public DateTime? ReadyForProvisioningAt { get; set; }
        public int? ReadyForProvisioningBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public int? BulkRequestId { get; set; }
        public string? RequestedByRole { get; set; }
        public StudentDto? Student { get; set; }
        public string? SubmittedByName { get; set; }
        public string? HousingReviewedByName { get; set; }
        public string? CyberReviewedByName { get; set; }
        public string? ReadyForProvisioningByName { get; set; }
        public string? CompletedByName { get; set; }
    }
}
