using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class WorkflowService : IWorkflowService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WorkflowService> _logger;

        public WorkflowService(AppDbContext context, ILogger<WorkflowService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAuditAsync(int? userId, string action, string targetTable, int targetId, string? ipAddress = null, string? userAgent = null)
        {
            var audit = new AuditLog
            {
                user_id = userId,
                action = action,
                target_table = targetTable,
                target_id = targetId,
                action_at = DateTime.UtcNow,
                ip_address = ipAddress,
                user_agent = userAgent
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Audit: User {UserId} {Action} on {Table}:{TargetId}",
                userId, action, targetTable, targetId);
        }

        public async Task<string?> GetPreviousStageAsync(int requestId)
        {
            var lastTwo = await _context.WorkflowHistories
                .Where(w => w.RequestId == requestId)
                .OrderByDescending(w => w.ActionDate)
                .Take(2)
                .ToListAsync();

            if (lastTwo.Count < 2)
                return null;

            return lastTwo[1].ToStage;
        }

        public async Task LogTransitionAsync(int requestId, string? fromStage, string toStage, int actionBy, string? notes = null, string? changesJson = null)
        {
            var history = new WorkflowHistory
            {
                RequestId = requestId,
                FromStage = fromStage,
                ToStage = toStage,
                ActionBy = actionBy,
                ActionDate = DateTime.UtcNow,
                Notes = notes,
                ChangesJson = changesJson
            };

            _context.WorkflowHistories.Add(history);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Workflow transition: Request {RequestId} {From} -> {To} by User {User}",
                requestId, fromStage ?? "(start)", toStage, actionBy);
        }

        // أحدث سجل يحمل تعديلات فعلية. الخطوات التي لا يصاحبها تعديل
        // (اعتماد، رفض، تحويل) لا تكتب changes_json فلا تظهر هنا.
        public async Task<(string? Json, DateTime? At)> GetLastChangesAsync(int requestId)
        {
            var last = await _context.WorkflowHistories
                .AsNoTracking()
                .Where(w => w.RequestId == requestId && w.ChangesJson != null)
                .OrderByDescending(w => w.ActionDate)
                .Select(w => new { w.ChangesJson, w.ActionDate })
                .FirstOrDefaultAsync();

            return (last?.ChangesJson, last?.ActionDate);
        }

        public async Task<List<WorkflowHistory>> GetHistoryAsync(int requestId)
        {
            return await _context.WorkflowHistories
                .Where(w => w.RequestId == requestId)
                .Include(w => w.Actor)
                .OrderBy(w => w.ActionDate)
                .ToListAsync();
        }

    }
}
