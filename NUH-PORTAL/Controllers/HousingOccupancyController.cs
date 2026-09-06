using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // خريطة مباني الإسكان - قراءة فقط.
    // ⚠️ الردّ فيه أسماء طلاب وأرقامهم الجامعية، فممنوع أي كاش: الرد نتيجة
    //    قرار صلاحية يخصّ المستخدم الحالي ونطاق قسمه.
    [Authorize(Policy = "housing.occupancyMap")]
    [Route("api/housing/occupancy")]
    [ApiController]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class HousingOccupancyController : ControllerBase
    {
        private readonly IHousingOccupancyService _service;

        public HousingOccupancyController(IHousingOccupancyService service) => _service = service;

        // GET api/housing/occupancy/buildings
        [HttpGet("buildings")]
        public async Task<IActionResult> GetBuildings() => Ok(await _service.GetBuildingsAsync());

        // GET api/housing/occupancy/{buildingId}
        [HttpGet("{buildingId:int}")]
        public async Task<IActionResult> GetBuilding(int buildingId) => Ok(await _service.GetBuildingAsync(buildingId));
    }
}
