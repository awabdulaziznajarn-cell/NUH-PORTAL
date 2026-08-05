using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Lookups;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // إدارة القوائم المرجعية + بنود التعهّد.
    // ⚠️ كانت Roles="admin" بينما شاشات القوائم نفسها بـ lookups.manage — يعني
    //    المشرف بيفتح الشاشة وكل حفظ بيرجّع 403. اتوحّدت على الصلاحية.
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "lookups.manage")]
    public class LookupAdminController : ControllerBase
    {
        private readonly ILookupAdminService _svc;
        public LookupAdminController(ILookupAdminService svc) => _svc = svc;

        // ===== lookups (college/department/building/academiclevel) =====
        [HttpGet("lookups/{category}")]
        public async Task<IActionResult> List(string category) => Ok(await _svc.ListAsync(category));

        [HttpPost("lookups/{category}")]
        public async Task<IActionResult> Create(string category, [FromBody] LookupSaveDto dto) => Ok(await _svc.SaveAsync(category, null, dto));

        [HttpPut("lookups/{category}/{id:int}")]
        public async Task<IActionResult> Update(string category, int id, [FromBody] LookupSaveDto dto) => Ok(await _svc.SaveAsync(category, id, dto));

        [HttpDelete("lookups/{category}/{id:int}")]
        public async Task<IActionResult> Delete(string category, int id)
        {
            await _svc.DeleteAsync(category, id);
            return Ok(new { deleted = true });
        }

        // ===== terms =====
        [HttpGet("terms")]
        public async Task<IActionResult> Terms() => Ok(await _svc.ListTermsAsync());

        [HttpPost("terms")]
        public async Task<IActionResult> CreateTerm([FromBody] TermSaveDto dto) => Ok(await _svc.SaveTermAsync(null, dto));

        [HttpPut("terms/{id:int}")]
        public async Task<IActionResult> UpdateTerm(int id, [FromBody] TermSaveDto dto) => Ok(await _svc.SaveTermAsync(id, dto));

        [HttpDelete("terms/{id:int}")]
        public async Task<IActionResult> DeleteTerm(int id)
        {
            await _svc.DeleteTermAsync(id);
            return Ok(new { deleted = true });
        }
    }
}
