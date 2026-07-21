using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Housing;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — إدارة حسابات AD في IHousingAccountService
    [Authorize(Roles = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class HousingAccountManagementController : ControllerBase
    {
        private readonly IHousingAccountService _service;

        public HousingAccountManagementController(IHousingAccountService service) => _service = service;

        // GET api/HousingAccountManagement?status=
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
            => Ok(await _service.GetAllAsync(status));

        // GET api/HousingAccountManagement/paged?page=&pageSize=&filterText=&sortBy=&sortAsc=&status=
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] QueryParams queryParams, [FromQuery] string? status = null)
            => Ok(await _service.GetPagedAsync(queryParams, status));

        // GET api/HousingAccountManagement/{studentId}
        [HttpGet("{studentId}")]
        public async Task<IActionResult> GetDetails(int studentId)
            => Ok(await _service.GetDetailsAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/enable
        [HttpPost("{studentId}/enable")]
        public async Task<IActionResult> EnableAccount(int studentId)
            => Ok(await _service.EnableAccountAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/disable
        [HttpPost("{studentId}/disable")]
        public async Task<IActionResult> DisableAccount(int studentId)
            => Ok(await _service.DisableAccountAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/reset-password
        [HttpPost("{studentId}/reset-password")]
        public async Task<IActionResult> ResetPassword(int studentId, [FromBody] ResetPasswordDto dto)
        {
            await _service.ResetPasswordAsync(studentId, dto);
            return Ok(new { message = "Password reset successfully" });
        }

        // POST api/HousingAccountManagement/{studentId}/re-provision
        [HttpPost("{studentId}/re-provision")]
        public async Task<IActionResult> ReProvision(int studentId)
        {
            var result = await _service.ReProvisionAsync(studentId);
            return Ok(new { message = result.Message, samAccountName = result.SamAccountName });
        }

        // POST api/HousingAccountManagement/{studentId}/sync-attrs
        [HttpPost("{studentId}/sync-attrs")]
        public async Task<IActionResult> SyncExtensionAttributes(int studentId)
        {
            await _service.SyncExtensionAttributesAsync(studentId);
            return Ok(new { message = "Extension attributes synced" });
        }

        // GET api/HousingAccountManagement/search?q=&max=
        [HttpGet("search")]
        public async Task<IActionResult> SearchADUsers([FromQuery] string? q, [FromQuery] int max = 50)
        {
            var result = await _service.SearchADUsersAsync(q, max);
            return Ok(new { users = result.Users, total = result.Total });
        }

        // GET api/HousingAccountManagement/lifecycle/{studentId}?limit=
        [HttpGet("lifecycle/{studentId}")]
        public async Task<IActionResult> GetLifecycleLogs(int studentId, [FromQuery] int limit = 50)
            => Ok(new { logs = await _service.GetLifecycleLogsAsync(studentId, limit) });

        // GET api/HousingAccountManagement/ad-config
        [HttpGet("ad-config")]
        public async Task<IActionResult> GetAdConfig()
            => Ok(new { configs = await _service.GetAdConfigAsync() });

        // POST api/HousingAccountManagement/ad-config
        [HttpPost("ad-config")]
        public async Task<IActionResult> UpdateAdConfig([FromBody] List<AdConfigDto> configs)
        {
            await _service.UpdateAdConfigAsync(configs);
            return Ok(new { message = "Configuration updated" });
        }

        // GET api/HousingAccountManagement/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetHousingStats()
            => Ok(await _service.GetHousingStatsAsync());
    }
}
