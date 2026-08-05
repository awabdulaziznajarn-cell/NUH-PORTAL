using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Housing;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — إدارة حسابات AD في IHousingAccountService
    // الأساس: القراءة housing.view، والإجراءات اللي بتلمس الأكتف دايركتوري فعليًا
    // housing.manageAccounts، والمزامنة/الإعدادات housing.syncAd.
    [Authorize(Policy = "housing.view")]
    [Route("api/[controller]")]
    [ApiController]
    public class HousingAccountManagementController : ControllerBase
    {
        private readonly IHousingAccountService _service;

        public HousingAccountManagementController(IHousingAccountService service) => _service = service;

        // POST api/HousingAccountManagement/refresh-status
        // «تحديث الحالات من الدومين» — بيقرا حالة الحسابات المربوطة بالفعل ويحدّثها.
        // مابيربطش أي حساب جديد، فمالوش أي خطر على بيانات الربط.
        [Authorize(Policy = "housing.syncAd")]
        [HttpPost("refresh-status")]
        public async Task<IActionResult> RefreshStatus()
            => Ok(await _service.SyncAdAccountsAsync(AdSyncMode.RefreshLinked, dryRun: false));

        // POST api/HousingAccountManagement/link-existing?dryRun=true
        // «ربط الحسابات» — بيدوّر على h + الرقم الجامعي للطلاب اللي لسه مالهمش حساب
        // مسجّل في النظام ويربطهم. قراءة فقط من الأكتف دايركتوري: مفيش إنشاء ولا تعطيل.
        //
        // ⚠️ dryRun افتراضيًا true: الربط الغلط (رقم جامعي غلط في الشيت) بيخلّي
        //    «تخرّج» بعد كده يعطّل حساب شخص تاني، فالمعاينة قبل الحفظ إجبارية
        //    من الواجهة — التنفيذ الفعلي لازم يتطلب صراحةً بـ dryRun=false.
        [Authorize(Policy = "housing.syncAd")]
        [HttpPost("link-existing")]
        public async Task<IActionResult> LinkExisting([FromQuery] bool dryRun = true)
            => Ok(await _service.SyncAdAccountsAsync(AdSyncMode.LinkNew, dryRun));

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
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/enable")]
        public async Task<IActionResult> EnableAccount(int studentId)
            => Ok(await _service.EnableAccountAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/disable
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/disable")]
        public async Task<IActionResult> DisableAccount(int studentId)
            => Ok(await _service.DisableAccountAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/reset-password
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/reset-password")]
        public async Task<IActionResult> ResetPassword(int studentId, [FromBody] ResetPasswordDto dto)
        {
            await _service.ResetPasswordAsync(studentId, dto);
            return Ok(new { message = "Password reset successfully" });
        }

        // POST api/HousingAccountManagement/{studentId}/re-provision
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/re-provision")]
        public async Task<IActionResult> ReProvision(int studentId)
        {
            var result = await _service.ReProvisionAsync(studentId);
            return Ok(new { message = result.Message, samAccountName = result.SamAccountName });
        }

        // POST api/HousingAccountManagement/{studentId}/sync-attrs
        [Authorize(Policy = "housing.syncAd")]
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
        [Authorize(Policy = "housing.syncAd")]
        [HttpGet("ad-config")]
        public async Task<IActionResult> GetAdConfig()
            => Ok(new { configs = await _service.GetAdConfigAsync() });

        // POST api/HousingAccountManagement/ad-config
        [Authorize(Policy = "housing.syncAd")]
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
