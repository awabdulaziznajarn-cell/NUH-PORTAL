using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NotificationsController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications([FromQuery] string? role)
        {
            var query = _context.Notifications.AsQueryable();
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.recipient_role == role);
            return await query.OrderByDescending(n => n.sent_at).Take(50).ToListAsync();
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<object>> GetUnreadCount([FromQuery] string? role)
        {
            var query = _context.Notifications.AsQueryable();
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.recipient_role == role);
            var count = await query.CountAsync(n => n.status == "pending");
            return new { count };
        }

        [HttpPatch("read")]
        public async Task<IActionResult> MarkAsRead([FromBody] List<int> ids)
        {
            if (ids == null || ids.Count == 0) return BadRequest();
            var notifications = await _context.Notifications
                .Where(n => ids.Contains(n.Id))
                .ToListAsync();
            foreach (var n in notifications)
                n.status = "read";
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}