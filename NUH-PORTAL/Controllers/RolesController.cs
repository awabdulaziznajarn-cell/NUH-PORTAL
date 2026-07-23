using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Roles;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // إدارة الأدوار وصلاحياتها — محميّة بصلاحيات roles.view / roles.manage.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly IRoleAdminService _service;

        public RolesController(IRoleAdminService service) => _service = service;

        // GET api/Roles
        [HttpGet]
        [Authorize(Policy = "roles.view")]
        public async Task<IActionResult> GetRoles()
            => Ok(await _service.GetRolesAsync());

        // GET api/Roles/permissions — كتالوج الصلاحيات مقسّم لمجموعات
        [HttpGet("permissions")]
        [Authorize(Policy = "roles.view")]
        public IActionResult GetPermissions()
            => Ok(_service.GetPermissionCatalog());

        // GET api/Roles/{id}
        [HttpGet("{id:int}")]
        [Authorize(Policy = "roles.view")]
        public async Task<IActionResult> GetRole(int id)
            => Ok(await _service.GetRoleAsync(id));

        // POST api/Roles
        [HttpPost]
        [Authorize(Policy = "roles.manage")]
        public async Task<IActionResult> Create([FromBody] RoleSaveDto dto)
            => Ok(await _service.CreateRoleAsync(dto));

        // PUT api/Roles/{id}
        [HttpPut("{id:int}")]
        [Authorize(Policy = "roles.manage")]
        public async Task<IActionResult> Update(int id, [FromBody] RoleSaveDto dto)
            => Ok(await _service.UpdateRoleAsync(id, dto));

        // DELETE api/Roles/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "roles.manage")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteRoleAsync(id);
            return Ok(new { deleted = true });
        }
    }
}
