using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.ADSetup;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — أدوات تشخيص AD في IADSetupService.
    // ملحوظة أمنية: بينشئ/يحذف مستخدمين حقيقيين في AD — صلاحية مستقلة (system.adSetup)
    // مش مربوطة بدور، الافتراضي إنها للأدمن بس.
    [Route("api/ad-setup")]
    [ApiController]
    [Authorize(Policy = "system.adSetup")]
    public class ADSetupController : ControllerBase
    {
        private readonly IADSetupService _service;

        public ADSetupController(IADSetupService service) => _service = service;

        // GET api/ad-setup/readiness
        [HttpGet("readiness")]
        public async Task<IActionResult> GetReadiness()
            => Ok(await _service.GetReadinessAsync());

        // GET api/ad-setup/dry-run/{studentId}
        [HttpGet("dry-run/{studentId}")]
        public async Task<IActionResult> GetDryRun(string studentId)
            => Ok(await _service.GetDryRunAsync(studentId));

        // POST api/ad-setup/test-user
        [HttpPost("test-user")]
        public async Task<IActionResult> PostTestUser([FromBody] ADTestUserRequest request)
            => Ok(await _service.RunTestUserAsync(request));
    }
}
