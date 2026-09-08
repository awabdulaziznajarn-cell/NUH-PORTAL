using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // ========================================================================
    //  إشغال غرفة واحدة - «الغرفة دي فيها كام؟»
    //
    //  ⚠️ نقطة مستقلّة عن خريطة المباني عن قصد: الخريطة بترجّع **أسماء**
    //     الساكنين وأرقامهم الجامعية، ودي معلومة ليها صلاحيتها. اللي بيسكّن
    //     طالبًا محتاج العدد وبس - يعرف الغرفة مليانة قبل ما يقرّر لا بعد ما
    //     الحفظ يترفض. لو ربطناها بصلاحية الخريطة، مشرف الإسكان اللي مالوش
    //     خريطة كان هيلاقي السطر مكسور في وشّه.
    //
    //  ⚠️ وممنوع أي كاش: الرقم بيتغيّر مع كل تسكين، ورقم قديم من الكاش معناه
    //     مشرف بيسكّن في غرفة اتملت من دقيقة.
    // ========================================================================
    [Authorize(Policy = "housing.roomInfo")]
    [Route("api/housing/room")]
    [ApiController]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class HousingRoomController : ControllerBase
    {
        private readonly IHousingCapacityGuard _capacity;

        public HousingRoomController(IHousingCapacityGuard capacity) => _capacity = capacity;

        // GET api/housing/room?building=66&floor=1&apartment=5&room=17[&excludeStudentId=12]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? building, [FromQuery] string? floor,
                                             [FromQuery] string? apartment, [FromQuery] string? room,
                                             [FromQuery] int? excludeStudentId)
        {
            // ⚠️ بلا رقم غرفة = «هات غرف الشقة كلها»: الشاشة بتعرض إشغال الأربعة
            //    جنب أرقامهم في القائمة، فبتسأل مرة واحدة لمّا الشقة تتغيّر لا
            //    مرة لكل غرفة.
            if (string.IsNullOrWhiteSpace(room))
            {
                var apt = await _capacity.GetApartmentAsync(building, floor, apartment, excludeStudentId);
                if (apt == null) return NoContent();

                return Ok(new
                {
                    capacity = apt.Capacity,
                    capacityMax = apt.CapacityMax,
                    rooms = apt.Rooms.Select(r => new { room = r.Room, occupied = r.Occupied })
                });
            }

            var info = await _capacity.GetRoomAsync(building, floor, apartment, room, excludeStudentId);
            if (info == null) return NoContent();

            return Ok(new { capacity = info.Capacity, capacityMax = info.CapacityMax, occupied = info.Occupied });
        }
    }
}
