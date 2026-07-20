using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services
{
    public class WorkflowService
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

        public async Task LogTransitionAsync(int requestId, string? fromStage, string toStage, int actionBy, string? notes = null)
        {
            var history = new WorkflowHistory
            {
                RequestId = requestId,
                FromStage = fromStage,
                ToStage = toStage,
                ActionBy = actionBy,
                ActionDate = DateTime.UtcNow,
                Notes = notes
            };

            _context.WorkflowHistories.Add(history);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Workflow transition: Request {RequestId} {From} -> {To} by User {User}",
                requestId, fromStage ?? "(start)", toStage, actionBy);
        }

        public async Task<List<WorkflowHistory>> GetHistoryAsync(int requestId)
        {
            return await _context.WorkflowHistories
                .Where(w => w.RequestId == requestId)
                .Include(w => w.Actor)
                .OrderBy(w => w.ActionDate)
                .ToListAsync();
        }

        public async Task<string?> GetCurrentStageAsync(int requestId)
        {
            var last = await _context.WorkflowHistories
                .Where(w => w.RequestId == requestId)
                .OrderByDescending(w => w.ActionDate)
                .FirstOrDefaultAsync();

            return last?.ToStage;
        }
    }
}
