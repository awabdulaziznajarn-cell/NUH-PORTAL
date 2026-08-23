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
    //
    // ⚠️ housing.view اتنقلت من هنا لكل أكشن على حدة، ومفيش أكشن اتغيّرت صلاحيته
    //    غير stats وحده. السبب: [Authorize] على الكلاس و[Authorize] على الأكشن
    //    الاتنين لازم ينجحوا معًا (AND لا OR)، فما كانش ينفع نخفّف stats وهي تحته.
    //    وstats محتاجة تخفيف لأن دونات لوحة التحكم أداة الأمن السيبراني، وهو
    //    مالوش housing.view — فالطلب كان بيرجع 403 والرسم مايتبنيش.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HousingAccountManagementController : ControllerBase
    {
        private readonly IHousingAccountService _service;

        public HousingAccountManagementController(IHousingAccountService service) => _service = service;

        // POST api/HousingAccountManagement/refresh-status
        // «تحديث الحالات من الدومين» — بيقرا حالة الحسابات المربوطة بالفعل ويحدّثها.
        // مابيربطش أي حساب جديد، فمالوش أي خطر على بيانات الربط.
        [Authorize(Policy = "housing.view")]
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
        [Authorize(Policy = "housing.view")]
        [Authorize(Policy = "housing.syncAd")]
        [HttpPost("link-existing")]
        public async Task<IActionResult> LinkExisting([FromQuery] bool dryRun = true)
            => Ok(await _service.SyncAdAccountsAsync(AdSyncMode.LinkNew, dryRun));

        // GET api/HousingAccountManagement?status=
        [Authorize(Policy = "housing.view")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
            => Ok(await _service.GetAllAsync(status));

        // GET api/HousingAccountManagement/paged?page=&pageSize=&filterText=&sortBy=&sortAsc=&status=
        [Authorize(Policy = "housing.view")]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] QueryParams queryParams, [FromQuery] string? status = null)
            => Ok(await _service.GetPagedAsync(queryParams, status));

        // GET api/HousingAccountManagement/{studentId}
        [Authorize(Policy = "housing.view")]
        [HttpGet("{studentId}")]
        public async Task<IActionResult> GetDetails(int studentId)
            => Ok(await _service.GetDetailsAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/enable
        [Authorize(Policy = "housing.view")]
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/enable")]
        public async Task<IActionResult> EnableAccount(int studentId)
            => Ok(await _service.EnableAccountAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/disable
        [Authorize(Policy = "housing.view")]
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/disable")]
        public async Task<IActionResult> DisableAccount(int studentId)
            => Ok(await _service.DisableAccountAsync(studentId));

        // POST api/HousingAccountManagement/{studentId}/reset-password
        [Authorize(Policy = "housing.view")]
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/reset-password")]
        public async Task<IActionResult> ResetPassword(int studentId, [FromBody] ResetPasswordDto dto)
        {
            await _service.ResetPasswordAsync(studentId, dto);
            return Ok(new { message = "Password reset successfully" });
        }

        // POST api/HousingAccountManagement/{studentId}/re-provision
        [Authorize(Policy = "housing.view")]
        [Authorize(Policy = "housing.manageAccounts")]
        [HttpPost("{studentId}/re-provision")]
        public async Task<IActionResult> ReProvision(int studentId)
        {
            var result = await _service.ReProvisionAsync(studentId);
            return Ok(new { message = result.Message, samAccountName = result.SamAccountName });
        }

        // POST api/HousingAccountManagement/{studentId}/sync-attrs
        [Authorize(Policy = "housing.view")]
        [Authorize(Policy = "housing.syncAd")]
        [HttpPost("{studentId}/sync-attrs")]
        public async Task<IActionResult> SyncExtensionAttributes(int studentId)
        {
            await _service.SyncExtensionAttributesAsync(studentId);
            return Ok(new { message = "Extension attributes synced" });
        }

        // GET api/HousingAccountManagement/search?q=&max=
        [Authorize(Policy = "housing.view")]
        [HttpGet("search")]
        public async Task<IActionResult> SearchADUsers([FromQuery] string? q, [FromQuery] int max = 50)
        {
            var result = await _service.SearchADUsersAsync(q, max);
            return Ok(new { users = result.Users, total = result.Total });
        }

        // GET api/HousingAccountManagement/lifecycle/{studentId}?limit=
        [Authorize(Policy = "housing.view")]
        [HttpGet("lifecycle/{studentId}")]
        public async Task<IActionResult> GetLifecycleLogs(int studentId, [FromQuery] int limit = 50)
            => Ok(new { logs = await _service.GetLifecycleLogsAsync(studentId, limit) });

        // GET api/HousingAccountManagement/stats
        // ⚠️ الأكشن الوحيد اللي مش على housing.view: ثلاثة مجاميع (مفعّل/معطّل/
        //    بلا حساب) بتغذّي دونات لوحة التحكم. السياسة معرّفة في Program.cs.
        //    مفيش بيانات طالب في الرد — أرقام مجمّعة بس.
        [Authorize(Policy = "dashboard.accountStats")]
        [HttpGet("stats")]
        public async Task<IActionResult> GetHousingStats()
            => Ok(await _service.GetHousingStatsAsync());
    }
}
