using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Lookups;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // إدارة المباني — شاشة مستقلة. المنطق في ILookupAdminService (فئة "building").
    [ApiController]
    [Route("api/admin/buildings")]
    [Authorize(Policy = "lookups.manage")]
    public class BuildingsController : ControllerBase
    {
        private const string Cat = "building";
        private readonly ILookupAdminService _svc;
        public BuildingsController(ILookupAdminService svc) => _svc = svc;

        [HttpGet] public async Task<IActionResult> List() => Ok(await _svc.ListAsync(Cat));
        [HttpPost] public async Task<IActionResult> Create([FromBody] LookupSaveDto dto) => Ok(await _svc.SaveAsync(Cat, null, dto));
        [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, [FromBody] LookupSaveDto dto) => Ok(await _svc.SaveAsync(Cat, id, dto));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(Cat, id);
            return Ok(new { deleted = true });
        }
    }
}
