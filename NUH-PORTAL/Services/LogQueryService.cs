using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.DTOs.Logs;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // استعلامات سجلّي الأخطاء والدخول — قراءة فقط، بترتيب زمني تنازلي (الأحدث أولًا).
    public class LogQueryService : ILogQueryService
    {
        private readonly IRepository<ErrorLog> _errors;
        private readonly IRepository<SignInLog> _signIns;

        public LogQueryService(IRepository<ErrorLog> errors, IRepository<SignInLog> signIns)
        {
            _errors = errors;
            _signIns = signIns;
        }

        public async Task<PagedResult<ErrorLogItemDto>> GetErrorLogsAsync(int page, int pageSize, string? search, string? fromDate, string? toDate)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;

            var q = _errors.Query().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(e =>
                    (e.message != null && e.message.ToLower().Contains(s)) ||
                    (e.exception_type != null && e.exception_type.ToLower().Contains(s)) ||
                    (e.request_path != null && e.request_path.ToLower().Contains(s)) ||
                    (e.username != null && e.username.ToLower().Contains(s)));
            }
            if (DateTime.TryParse(fromDate, out var from)) q = q.Where(e => e.occurred_at >= from);
            if (DateTime.TryParse(toDate, out var to)) q = q.Where(e => e.occurred_at <= to);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(e => e.occurred_at)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(e => new ErrorLogItemDto
                {
                    Id = e.Id,
                    OccurredAt = e.occurred_at,
                    Message = e.message,
                    ExceptionType = e.exception_type,
                    StatusCode = e.status_code,
                    RequestPath = e.request_path,
                    RequestMethod = e.request_method,
                    Username = e.username,
                    IpAddress = e.ip_address
                })
                .ToListAsync();

            return new PagedResult<ErrorLogItemDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<ErrorLogDetailDto> GetErrorLogAsync(int id)
        {
            var dto = await _errors.Query().AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => new ErrorLogDetailDto
                {
                    Id = e.Id,
                    OccurredAt = e.occurred_at,
                    Message = e.message,
                    ExceptionType = e.exception_type,
                    StatusCode = e.status_code,
                    RequestPath = e.request_path,
                    RequestMethod = e.request_method,
                    Username = e.username,
                    IpAddress = e.ip_address,
                    StackTrace = e.stack_trace,
                    Source = e.source,
                    UserAgent = e.user_agent,
                    UserId = e.user_id
                })
                .FirstOrDefaultAsync();

            return dto ?? throw UserFriendlyException.NotFound("السجل غير موجود");
        }

        public async Task<PagedResult<SignInLogItemDto>> GetSignInLogsAsync(int page, int pageSize, string? eventType, string? search, string? fromDate, string? toDate)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;

            var q = _signIns.Query().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(eventType))
                q = q.Where(s => s.event_type == eventType);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(x =>
                    (x.username != null && x.username.ToLower().Contains(s)) ||
                    (x.ip_address != null && x.ip_address.Contains(s)) ||
                    (x.User != null && x.User.full_name != null && x.User.full_name.ToLower().Contains(s)));
            }
            if (DateTime.TryParse(fromDate, out var from)) q = q.Where(s => s.occurred_at >= from);
            if (DateTime.TryParse(toDate, out var to)) q = q.Where(s => s.occurred_at <= to);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(s => s.occurred_at)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new SignInLogItemDto
                {
                    Id = s.Id,
                    OccurredAt = s.occurred_at,
                    Username = s.username,
                    FullName = s.User != null ? s.User.full_name : null,
                    EventType = s.event_type,
                    Method = s.method,
                    Success = s.success,
                    Detail = s.detail,
                    IpAddress = s.ip_address,
                    UserAgent = s.user_agent
                })
                .ToListAsync();

            return new PagedResult<SignInLogItemDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }
    }
}
