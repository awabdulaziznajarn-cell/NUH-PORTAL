using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Requests
{
    // نفس شكل الـ Request entity (اللي كان بيترجع زي ما هو) للحفاظ على الـ JSON
    public class RequestDto
    {
        public int Id { get; set; }
        public RequestType? RequestType { get; set; }
        public int StudentId { get; set; }
        public int? SubmittedBy { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public string? RequestedByRole { get; set; }
        public int? HousingReviewedBy { get; set; }
        public DateTime? HousingReviewedAt { get; set; }
        public string? HousingNotes { get; set; }
        public int? CyberReviewedBy { get; set; }
        public DateTime? CyberReviewedAt { get; set; }
        public string? CyberNotes { get; set; }
        public DateTime? ReadyForProvisioningAt { get; set; }
        public int? ReadyForProvisioningBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public int? BulkRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public string? RegistrationData { get; set; }
        public StudentDto? Student { get; set; }
    }
}
