using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Bulk;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق التسجيل الجماعي في IBulkRegistrationService
    // كانت بالدور (admin,supervisor)، فأي دور جديد ياخد صلاحية الرفع الجماعي
    // كان بيلاقي الشاشة مفتوحة والـ API بيرفض. بقت بالصلاحية.
    [Authorize(Policy = "students.bulkImport")]
    [Route("api/[controller]")]
    [ApiController]
    public class BulkRegistrationController : ControllerBase
    {
        private readonly IBulkRegistrationService _service;

        public BulkRegistrationController(IBulkRegistrationService service) => _service = service;

        // GET api/BulkRegistration/template
        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            var file = _service.GetTemplate();
            return File(file.Content, file.ContentType, file.FileName);
        }

        // POST api/BulkRegistration/validate (multipart)
        [HttpPost("validate")]
        [RequestSizeLimit(10L * 1024 * 1024)]
        public async Task<IActionResult> ValidateFile(IFormFile file)
            => Ok(await _service.ValidateFileAsync(file));

        // POST api/BulkRegistration/create
        [HttpPost("create")]
        public async Task<IActionResult> CreateBulkRequest([FromBody] BulkCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return Ok(new { id = result.Id, requestId = result.RequestId, requestNumber = result.RequestNumber, recordCount = result.RecordCount });
        }

        // GET api/BulkRegistration
        [HttpGet]
        public async Task<IActionResult> GetBulkRequests()
            => Ok(await _service.GetAllAsync());

        // GET api/BulkRegistration/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBulkRequest(int id)
            => Ok(await _service.GetByIdAsync(id));
    }
}
