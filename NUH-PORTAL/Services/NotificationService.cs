using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Notifications;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class NotificationService : AppServiceBase, INotificationService
    {
        private readonly IRepository<Notification> _notifications;

        public NotificationService(IRepository<Notification> notifications, IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
            _notifications = notifications;
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(string? role)
        {
            var query = _notifications.Query().AsNoTracking();
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.recipient_role == role);

            var list = await query
                .OrderByDescending(n => n.sent_at)
                .Take(50)
                .ToListAsync();

            return Mapper.Map<List<NotificationDto>>(list);
        }

        public async Task<int> GetUnreadCountAsync(string? role)
        {
            var query = _notifications.Query().AsNoTracking();
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.recipient_role == role);

            return await query.CountAsync(n => n.status == "pending");
        }

        public async Task MarkAsReadAsync(List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                throw new UserFriendlyException("قائمة الإشعارات مطلوبة", 400);

            var notifications = await _notifications.FindAllAsync(n => ids.Contains(n.Id));
            foreach (var n in notifications)
                n.status = "read";

            await UnitOfWork.SaveAsync();
        }
    }
}
