using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Requests;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — كل منطق دورة حياة الطلب في IRequestService
    [Authorize]
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

        // GET api/Requests/paged?page=&pageSize=&filterText=&sortBy=&sortAsc=&status=&requestType=
        [HttpGet("paged")]
        public async Task<IActionResult> GetRequestsPaged([FromQuery] QueryParams queryParams, [FromQuery] string? status = null, [FromQuery] string? requestType = null)
            => Ok(await _service.GetPagedAsync(queryParams, status, requestType));

        // GET api/Requests/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRequest(int id)
            => Ok(await _service.GetDetailsAsync(id));

        // GET api/Requests/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
            => Ok(await _service.GetPendingAsync());

        // POST api/Requests
        [HttpPost]
        public async Task<IActionResult> CreateRequest([FromBody] RequestCreateDto dto)
            => Ok(await _service.CreateAsync(dto));

        // PUT api/Requests/{id}/review
        [Authorize(Roles = "admin,supervisor,cyber")]
        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewRequest(int id, [FromBody] ReviewDto dto)
            => Ok(await _service.ReviewAsync(id, dto));

        // PATCH api/Requests/{id}
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateRequest(int id, [FromBody] UpdateRequestDto dto)
            => Ok(await _service.UpdateBulkIdAsync(id, dto));
    }
}
