using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Lookups;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // إدارة بنود التعهّد — شاشة مستقلة. المنطق في ILookupAdminService (دوال الـ terms).
    [ApiController]
    [Route("api/admin/pledge-terms")]
    [Authorize(Policy = "lookups.manage")]
    public class PledgeTermsController : ControllerBase
    {
        private readonly ILookupAdminService _svc;
        public PledgeTermsController(ILookupAdminService svc) => _svc = svc;

        [HttpGet] public async Task<IActionResult> List() => Ok(await _svc.ListTermsAsync());
        [HttpPost] public async Task<IActionResult> Create([FromBody] TermSaveDto dto) => Ok(await _svc.SaveTermAsync(null, dto));
        [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, [FromBody] TermSaveDto dto) => Ok(await _svc.SaveTermAsync(id, dto));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteTermAsync(id);
            return Ok(new { deleted = true });
        }
    }
}
