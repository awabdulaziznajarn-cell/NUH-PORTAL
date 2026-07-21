using NUH_PORTAL.DTOs.Notifications;

namespace NUH_PORTAL.Services.Interfaces
{
    public interface INotificationService
    {
        Task<List<NotificationDto>> GetNotificationsAsync(string? role);
        Task<int> GetUnreadCountAsync(string? role);
        Task MarkAsReadAsync(List<int> ids);
    }
}
