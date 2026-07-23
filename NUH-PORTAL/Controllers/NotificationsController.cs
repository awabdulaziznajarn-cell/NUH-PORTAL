using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — المنطق في INotificationService
    // الدور بيتاخد من هوية المستخدم (claims) مش من الـ query — عشان محدش يقرا إشعارات دور تاني.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationsController(INotificationService service) => _service = service;

        private string? CurrentRole() => User.FindFirst(ClaimTypes.Role)?.Value;

        // GET api/Notifications
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
            => Ok(await _service.GetNotificationsAsync(CurrentRole()));

        // GET api/Notifications/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
            => Ok(new { count = await _service.GetUnreadCountAsync(CurrentRole()) });

        // PATCH api/Notifications/read
        [HttpPatch("read")]
        public async Task<IActionResult> MarkAsRead([FromBody] List<int> ids)
        {
            await _service.MarkAsReadAsync(ids);
            return Ok();
        }
    }
}
