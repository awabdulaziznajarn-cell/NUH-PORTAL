using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — المنطق في INotificationService
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationsController(INotificationService service) => _service = service;

        // GET api/Notifications?role=
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] string? role)
            => Ok(await _service.GetNotificationsAsync(role));

        // GET api/Notifications/unread-count?role=
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount([FromQuery] string? role)
            => Ok(new { count = await _service.GetUnreadCountAsync(role) });

        // PATCH api/Notifications/read
        [HttpPatch("read")]
        public async Task<IActionResult> MarkAsRead([FromBody] List<int> ids)
        {
            await _service.MarkAsReadAsync(ids);
            return Ok();
        }
    }
}
