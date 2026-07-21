using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — المنطق في IUserService
    [Authorize(Roles = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service) => _service = service;

        // GET api/Users
        [HttpGet]
        public async Task<IActionResult> GetUsers()
            => Ok(await _service.GetUsersAsync());

        // GET api/Users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
            => Ok(await _service.GetUserAsync(id));
    }
}
