using Microsoft.AspNetCore.Http;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // بيكتب AuditLog (+ تغييرات اختيارية). بيقرا هوية المستخدم من الـ UoW والـ IP/User-Agent من الـ HttpContext
    public class AuditService : IAuditService
    {
        private readonly IRepository<AuditLog> _auditLogs;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _http;

        public AuditService(IRepository<AuditLog> auditLogs, IUnitOfWork unitOfWork, IHttpContextAccessor http)
        {
            _auditLogs = auditLogs;
            _unitOfWork = unitOfWork;
            _http = http;
        }

        public async Task LogAsync(string action, string targetTable, int targetId, List<AuditChangeLog>? changes = null)
        {
            var userId = _unitOfWork.GetCurrentUserId();
            if (userId <= 0) return; // نفس سلوك الكنترولر القديم: مفيش تسجيل من غير مستخدم معروف

            var ctx = _http.HttpContext;
            var log = new AuditLog
            {
                user_id = userId,
                action = action,
                target_table = targetTable,
                target_id = targetId,
                action_at = DateTime.UtcNow,
                ip_address = ctx?.Connection.RemoteIpAddress?.ToString(),
                user_agent = ctx?.Request.Headers["User-Agent"].ToString(),
                AuditChangeLogs = changes
            };

            await _auditLogs.AddAsync(log);
            await _unitOfWork.SaveAsync();
        }
    }
}
