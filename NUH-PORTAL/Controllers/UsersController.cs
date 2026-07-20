using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UsersController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
            => await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.username,
                    u.full_name,
                    u.email,
                    u.role,
                    u.created_at,
                    u.is_active
                })
                .ToListAsync();

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.username,
                    u.full_name,
                    u.email,
                    u.role,
                    u.created_at,
                    u.is_active
                })
                .FirstOrDefaultAsync();

            return user == null ? NotFound() : Ok(user);
        }
    }
}