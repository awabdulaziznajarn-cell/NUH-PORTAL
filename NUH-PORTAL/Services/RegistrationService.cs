using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Services.Interfaces;
using System.Text.Json;

namespace NUH_PORTAL.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly AppDbContext _context;
        private readonly IWorkflowService _workflowService;
        private readonly ADProvisioningService _adProvisioning;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(AppDbContext context, IWorkflowService workflowService, ADProvisioningService adProvisioning, ILogger<RegistrationService> logger)
        {
            _context = context;
            _workflowService = workflowService;
            _adProvisioning = adProvisioning;
            _logger = logger;
        }

        public async Task<string> GenerateRequestNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"{year}-";

            var lastRequest = await _context.Requests
                .Where(r => r.RequestNumber != null && r.RequestNumber.StartsWith(prefix))
                .OrderByDescending(r => r.RequestNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastRequest?.RequestNumber != null)
            {
                var parts = lastRequest.RequestNumber.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out var lastSeq))
                    nextSeq = lastSeq + 1;
            }

            return $"{prefix}{nextSeq:D6}";
        }

        private static string NormalizePhone(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return mobile;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 10 && digits.StartsWith("05"))
                return "9665" + digits[2..];
            if (digits.Length == 9 && digits.StartsWith("5"))
                return "966" + digits;
            return digits;
        }

        private static bool PhonesMatch(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return NormalizePhone(a) == NormalizePhone(b);
        }

        public async Task<bool> CheckDuplicateByMobileAsync(string mobile, int? excludeRequestId = null)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return false;

            var activeStatuses = new[] { "pending_supervisor", "pending_cyber", "ready_for_provisioning", "need_more_info" };

            var requests = await _context.Requests
                .Include(r => r.Student)
                .Where(r => r.RequestType == RequestType.self_registration && activeStatuses.Contains(r.Status))
                .ToListAsync();

            foreach (var req in requests)
            {
                if (excludeRequestId.HasValue && req.Id == excludeRequestId.Value)
                    continue;

                if (!string.IsNullOrEmpty(req.RegistrationData))
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(req.RegistrationData);
                        if (data.TryGetProperty("mobile", out var mobileProp))
                        {
                            var reqMobile = mobileProp.GetString();
                            if (PhonesMatch(reqMobile, mobile))
                                return true;
                        }
                    }
                    catch { }
                }

                if (req.Student != null && !string.IsNullOrEmpty(req.Student.phone) && PhonesMatch(req.Student.phone, mobile))
                    return true;
            }

            return false;
        }

        public async Task<bool> CheckDuplicateByStudentIdAsync(string studentId, int? excludeRequestId = null)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                return false;

            var activeStatuses = new[] { "pending_supervisor", "pending_cyber", "ready_for_provisioning", "need_more_info" };

            var student = await _context.Students.FirstOrDefaultAsync(s => s.student_id == studentId);
            if (student == null)
                return false;

            var query = _context.Requests
                .Where(r => r.StudentId == student.Id && r.RequestType == RequestType.self_registration && activeStatuses.Contains(r.Status));

            if (excludeRequestId.HasValue)
                query = query.Where(r => r.Id != excludeRequestId.Value);

            return await query.AnyAsync();
        }

        public async Task<Request> CreateRegistrationRequestAsync(int studentId, string requestNumber, string registrationData, int submittedBy)
        {
            var request = new Request
            {
                RequestType = RequestType.self_registration,
                StudentId = studentId,
                SubmittedBy = submittedBy,
                Status = "pending_supervisor",
                RequestNumber = requestNumber,
                RegistrationData = registrationData,
                SubmittedAt = DateTime.UtcNow
            };

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            await _workflowService.LogTransitionAsync(request.Id, null, "pending_supervisor", submittedBy, "تقديم طلب التسجيل");

            _context.Notifications.Add(new Notification
            {
                request_id = request.Id,
                channel = "in_app",
                recipient_role = "supervisor",
                message = $"تم تقديم طلب تسجيل جديد ({requestNumber})",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return request;
        }

        public async Task<bool> ApproveAsSupervisorAsync(int requestId, int supervisorId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_supervisor")
                return false;

            request.Status = "pending_cyber";
            request.ReviewedBy = supervisorId;
            request.ReviewedAt = DateTime.UtcNow;
            request.Notes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_supervisor", "pending_cyber", supervisorId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "cyber",
                message = $"تمت الموافقة على طلب التسجيل ({reqNum}) من قبل إدارة الإسكان",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectAsSupervisorAsync(int requestId, int supervisorId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_supervisor")
                return false;

            request.Status = "rejected";
            request.ReviewedBy = supervisorId;
            request.ReviewedAt = DateTime.UtcNow;
            request.Notes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_supervisor", "rejected", supervisorId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم رفض طلب التسجيل ({reqNum}) من قبل إدارة الإسكان",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ApproveAsCyberAsync(int requestId, int cyberId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_cyber")
                return false;

            request.Status = "ready_for_provisioning";
            request.CyberReviewedBy = cyberId;
            request.CyberReviewedAt = DateTime.UtcNow;
            request.CyberNotes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_cyber", "ready_for_provisioning", cyberId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تمت الموافقة على طلب التسجيل ({reqNum}) من قبل إدارة الأمن السيبراني",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectAsCyberAsync(int requestId, int cyberId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_cyber")
                return false;

            request.Status = "rejected";
            request.CyberReviewedBy = cyberId;
            request.CyberReviewedAt = DateTime.UtcNow;
            request.CyberNotes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_cyber", "rejected", cyberId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم رفض طلب التسجيل ({reqNum}) من قبل إدارة الأمن السيبراني",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ApproveAsAdminAsync(int requestId, int adminId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "ready_for_provisioning")
                return false;

            request.HousingReviewedBy = adminId;
            request.HousingReviewedAt = DateTime.UtcNow;
            request.HousingNotes = notes;
            request.ReadyForProvisioningBy = adminId;
            request.ReadyForProvisioningAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var student = await _context.Students.FindAsync(request.StudentId);
            if (student != null)
            {
                var provResult = await _adProvisioning.ProvisionAsync(student, adminId);
                if (!provResult.Success)
                {
                    _logger.LogError("AD provisioning FAILED for self-registration student {Id}: {Error}", student.student_id, provResult.Error);
                    request.Status = "ready_for_provisioning";
                    await _context.SaveChangesAsync();
                    return false;
                }

                _logger.LogInformation("AD account created for self-registration student {Id}: {Sam}", student.student_id, provResult.SamAccountName);
                var syncResult = await _adProvisioning.SyncExtensionAttributesAsync(student, adminId);
                if (!syncResult.Success)
                    _logger.LogWarning("Extension attribute sync failed for student {Id}: {Error}", student.student_id, syncResult.Error);

                if (student.status != StudentState.left)
                    student.status = StudentState.active;
            }

            request.Status = "completed";
            request.CompletedBy = adminId;
            request.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "ready_for_provisioning", "completed", adminId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم إكمال طلب التسجيل ({reqNum}) وتم إنشاء حساب الشبكة",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectAsAdminAsync(int requestId, int adminId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "ready_for_provisioning")
                return false;

            request.Status = "rejected";
            request.HousingReviewedBy = adminId;
            request.HousingReviewedAt = DateTime.UtcNow;
            request.HousingNotes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "ready_for_provisioning", "rejected", adminId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم رفض طلب التسجيل ({reqNum}) من قبل الإدارة",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RequestMoreInfoAsync(int requestId, int reviewerId, string notes, string? fromStage = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null)
                return false;

            var currentStage = fromStage ?? request.Status;
            request.Status = "need_more_info";
            request.Notes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, currentStage, "need_more_info", reviewerId, notes);

            return true;
        }

        public async Task<string?> ResubmitRequestAsync(int requestId, int userId, string? registrationData = null)
        {
            var req = await _context.Requests.FindAsync(requestId);
            if (req == null || req.RequestType != RequestType.self_registration || req.Status != "need_more_info")
                return null;

            var previousStage = await _workflowService.GetPreviousStageAsync(requestId);
            var targetStage = previousStage ?? "pending_supervisor";

            if (!string.IsNullOrEmpty(registrationData))
                req.RegistrationData = registrationData;

            req.Status = targetStage;
            req.ReviewedAt = null;
            req.ReviewedBy = null;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "need_more_info", targetStage, userId, "إعادة تقديم بعد طلب معلومات");

            return targetStage;
        }
    }
}
