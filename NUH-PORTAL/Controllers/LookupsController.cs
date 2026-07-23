using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // endpoints القوائم المنسدلة — بيانات مرجعية غير حسّاسة، متاحة للفورم
    [ApiController]
    [Route("api/lookups")]
    public class LookupsController : ControllerBase
    {
        private readonly ILookupService _svc;
        public LookupsController(ILookupService svc) => _svc = svc;

        [HttpGet("colleges"), AllowAnonymous]
        public async Task<IActionResult> Colleges() => Ok(await _svc.GetCollegesAsync());

        [HttpGet("departments"), AllowAnonymous]
        public async Task<IActionResult> Departments([FromQuery] int? collegeId) => Ok(await _svc.GetDepartmentsAsync(collegeId));

        [HttpGet("buildings"), AllowAnonymous]
        public async Task<IActionResult> Buildings([FromQuery] Gender? gender) => Ok(await _svc.GetBuildingsAsync(gender));

        [HttpGet("academic-levels"), AllowAnonymous]
        public async Task<IActionResult> AcademicLevels() => Ok(await _svc.GetAcademicLevelsAsync());

        [HttpGet("terms"), AllowAnonymous]
        public async Task<IActionResult> Terms() => Ok(await _svc.GetTermsAsync());
    }
}
