using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Workflow;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // منطق طوابير الـ workflow — بيستخدم RegistrationService للانتقالات و WorkflowService للسجل
    public class WorkflowActionService : AppServiceBase, IWorkflowActionService
    {
        private readonly IRepository<Request> _requests;
        private readonly IRegistrationService _registration;
        private readonly IWorkflowService _workflow;
        private readonly IAuditService _audit;

        public WorkflowActionService(
            IRepository<Request> requests,
            IRegistrationService registration,
            IWorkflowService workflow,
            IAuditService audit,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _requests = requests;
            _registration = registration;
            _workflow = workflow;
            _audit = audit;
        }

        public async Task<List<QueueItemDto>> GetQueueAsync(string? stage)
        {
            var userRole = UnitOfWork.GetCurrentUserRole();

            var targetStage = stage;
            if (string.IsNullOrEmpty(targetStage))
            {
                targetStage = userRole switch
                {
                    "supervisor" => "pending_supervisor",
                    "cyber" => "pending_cyber",
                    "admin" => "ready_for_provisioning",
                    _ => null
                };
            }

            if (string.IsNullOrEmpty(targetStage))
                throw new UserFriendlyException("لا يمكن تحديد المرحلة", 400);

            return await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .Where(r => r.RequestType == "self_registration" && r.Status == targetStage)
                .OrderBy(r => r.SubmittedAt)
                .Select(r => new QueueItemDto
                {
                    Id = r.Id,
                    RequestNumber = r.RequestNumber,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    StudentName = r.Student!.full_name,
                    StudentId = r.Student.student_id,
                    Notes = r.Notes
                })
                .ToListAsync();
        }

        public async Task<List<StatusCountDto>> GetQueueCountsAsync()
        {
            return await _requests.Query().AsNoTracking()
                .Where(r => r.RequestType == "self_registration")
                .GroupBy(r => r.Status)
                .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
                .ToListAsync();
        }

        public async Task ApproveAsync(int requestId, string? notes)
        {
            var (actorId, userRole) = GetActor();

            var result = userRole switch
            {
                "supervisor" => await _registration.ApproveAsSupervisorAsync(requestId, actorId, notes),
                "cyber" => await _registration.ApproveAsCyberAsync(requestId, actorId, notes),
                "admin" => await _registration.ApproveAsAdminAsync(requestId, actorId, notes),
                _ => false
            };

            if (!result)
                throw new UserFriendlyException("لا يمكن اعتماد الطلب في المرحلة الحالية", 400);

            await _audit.LogAsync("workflow_approved", "Requests", requestId);
        }

        public async Task RejectAsync(int requestId, string? notes)
        {
            var (actorId, userRole) = GetActor();

            var result = userRole switch
            {
                "supervisor" => await _registration.RejectAsSupervisorAsync(requestId, actorId, notes),
                "cyber" => await _registration.RejectAsCyberAsync(requestId, actorId, notes),
                "admin" => await _registration.RejectAsAdminAsync(requestId, actorId, notes),
                _ => false
            };

            if (!result)
                throw new UserFriendlyException("لا يمكن رفض الطلب في المرحلة الحالية", 400);

            await _audit.LogAsync("workflow_rejected", "Requests", requestId);
        }

        public async Task RequestMoreInfoAsync(int requestId, string? notes)
        {
            var (actorId, _) = GetActor();

            if (string.IsNullOrWhiteSpace(notes))
                throw new UserFriendlyException("الملاحظات مطلوبة لطلب معلومات إضافية", 400);

            var result = await _registration.RequestMoreInfoAsync(requestId, actorId, notes);

            if (!result)
                throw new UserFriendlyException("لا يمكن طلب معلومات إضافية لهذا الطلب", 400);

            await _audit.LogAsync("info_requested", "Requests", requestId);
        }

        public async Task<List<WorkflowHistoryItemDto>> GetHistoryAsync(int requestId)
        {
            var request = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == requestId);
            var studentName = request?.Student?.full_name;

            var history = await _workflow.GetHistoryAsync(requestId);
            return history.Select(h => new WorkflowHistoryItemDto
            {
                FromStage = h.FromStage,
                ToStage = h.ToStage,
                ActionDate = h.ActionDate,
                Notes = h.Notes,
                // لو المنفّذ طالب (دور user) بنعرض اسم الطالب صاحب الطلب — نفس منطق الكود القديم
                ActorName = h.Actor != null
                    ? (h.Actor.role == "user" && studentName != null ? studentName : h.Actor.full_name ?? h.Actor.username)
                    : null
            }).ToList();
        }

        // ----------------------------- Helpers -----------------------------

        private (int actorId, string? role) GetActor()
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            var role = UnitOfWork.GetCurrentUserRole();
            if (string.IsNullOrEmpty(role))
                throw new UserFriendlyException("غير مصرح", 401);

            return (actorId, role);
        }
    }
}
