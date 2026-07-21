using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // كنترولر رفيع — منطق النقل في ISupervisorHousingTransferService
    [Authorize(Roles = "supervisor")]
    [Route("api/supervisor/housing-transfer")]
    [ApiController]
    public class SupervisorHousingTransferController : ControllerBase
    {
        private readonly ISupervisorHousingTransferService _service;

        public SupervisorHousingTransferController(ISupervisorHousingTransferService service) => _service = service;

        // POST api/supervisor/housing-transfer (multipart)
        [HttpPost]
        [RequestSizeLimit(Services.SupervisorHousingTransferService.MaxFileSize)]
        public async Task<IActionResult> CreateTransfer(
            [FromForm] string studentNumber,
            [FromForm] string newBuilding,
            [FromForm] string newApartment,
            [FromForm] string newRoom,
            [FromForm] string reason,
            [FromForm] string? customReason,
            IFormFile? file)
        {
            var result = await _service.CreateTransferAsync(studentNumber, newBuilding, newApartment, newRoom, reason, customReason, file);
            return Ok(new { message = result.Message, transferId = result.TransferId, oldLocation = result.OldLocation, newLocation = result.NewLocation });
        }

        // GET api/supervisor/housing-transfer/recent
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
            => Ok(await _service.GetRecentAsync());
    }
}
