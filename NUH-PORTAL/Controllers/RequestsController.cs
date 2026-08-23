using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Requests;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — كل منطق دورة حياة الطلب في IRequestService
    // القراءة requests.view، والإنشاء/التعديل requests.create — عشان توكن OTP (دور user) ميقراش/يعدّلش كل الطلبات.
    // ⚠️ كان الاسم "requests.process" وهو مش معرّف في ApplicationPermissions،
    //    فما كانش ليه policy والرد كان 500 على الإنشاء والتعديل.
    [Authorize(Policy = "requests.view")]
    [Route("api/[controller]")]
    [ApiController]
    public class RequestsController : ControllerBase
    {
        private readonly IRequestService _service;

        public RequestsController(IRequestService service) => _service = service;

        // GET api/Requests
        [HttpGet]
        public async Task<IActionResult> GetRequests()
            => Ok(await _service.GetAllAsync());

        // GET api/Requests/paged?page=&pageSize=&filterText=&sortBy=&sortAsc=&status=&requestType=&mine=&from=&to=
        // filterText: خانة بحث واحدة تقارن برقم الطلب واسم الطالب ورقمه الجامعي معًا
        // from/to: مدى تاريخ التقديم بتوقيت السعودية (yyyy-MM-dd)
        // openOnly: الطلبات التي لم تُغلق بعد - تستعملها لوحة «الأطول انتظارًا» في الصفحة الرئيسية
        [HttpGet("paged")]
        public async Task<IActionResult> GetRequestsPaged([FromQuery] QueryParams queryParams, [FromQuery] string? status = null, [FromQuery] string? requestType = null, [FromQuery] bool mine = false, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] bool openOnly = false)
            => Ok(await _service.GetPagedAsync(queryParams, status, requestType, mine, from, to, openOnly));

        // GET api/Requests/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRequest(int id)
            => Ok(await _service.GetDetailsAsync(id));

        // GET api/Requests/stats — عدادات الحالات لصفحة إدارة الطلبات
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
            => Ok(await _service.GetStatsAsync());

        // POST api/Requests
        [Authorize(Policy = "requests.create")]
        [HttpPost]
        public async Task<IActionResult> CreateRequest([FromBody] RequestCreateDto dto)
            => Ok(await _service.CreateAsync(dto));

        // PUT api/Requests/{id}/review
        [Authorize(Roles = "admin,supervisor,cyber")]
        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewRequest(int id, [FromBody] ReviewDto dto)
            => Ok(await _service.ReviewAsync(id, dto));

        // PATCH api/Requests/{id}
        [Authorize(Policy = "requests.create")]
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateRequest(int id, [FromBody] UpdateRequestDto dto)
            => Ok(await _service.UpdateBulkIdAsync(id, dto));
    }
}
