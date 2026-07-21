using AutoMapper;
using NUH_PORTAL.DTOs.Notifications;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Mappings
{
    public class NotificationProfile : Profile
    {
        public NotificationProfile()
        {
            CreateMap<Notification, NotificationDto>();
        }
    }
}
