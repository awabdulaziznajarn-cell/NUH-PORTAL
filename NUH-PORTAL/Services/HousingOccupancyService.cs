using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Housing;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // ========================================================================
    //  خريطة إشغال المباني.
    //
    //  ⚠️ مافيش جدول غرف ولا عمود «عدد الساكنين». الإشغال بيتحسب في كل نداء
    //     من صفوف الطلاب: البنية والترقيم من Core/HousingStructure وعمود
    //     Numbering، والسعة من Buildings.RoomCapacity. يعني الخريطة مستحيل
    //     تفرق عن الحقيقة، ومافيش حاجة محتاجة «إعادة مزامنة» بعد أي نقل.
    //
    //  ⚠️ الربط بـ BuildingId لا بـ housing_building: التاني نصّ بيكتبه
    //     المستخدم، واللي بيربطه بالمبنى هو LookupResolver. صفّ فيه نصّ
    //     من غير مفتاح مش مربوط بمبنى أصلًا - وبيطلع في «صفوف خارج البنية»
    //     بدل ما يختفي بالصمت.
    // ========================================================================
    public class HousingOccupancyService : AppServiceBase, IHousingOccupancyService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Building> _buildings;

        public HousingOccupancyService(
            IRepository<Student> students,
            IRepository<Building> buildings,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _buildings = buildings;
        }

        // ⚠️ الحالة بترجع **لو كانت حالة مغادرة وبس**. الطالب المقيم عادي حالته
        //    «نشط»، ولو رجعت كانت هتتعامل في الشاشة كتنبيه: نقطة حمرا على كل
        //    غرفة مسكونة وشارة على كل ساكن - يعني العلامة اللي معناها «فيه خلل
        //    هنا» تبقى على كل حاجة، فتبطل تقول أي حاجة.
        //
        //    والحالات الأربع دي بالذات لأن كلها بتنهي السكن: صفّ لسه شايل غرفة
        //    ومعاه واحدة منها يبقى فيه تناقض محتاج يتصلّح.
        private static string? DepartureStatus(StudentStatus? st)
            => st is StudentStatus.graduated or StudentStatus.dismissed
                  or StudentStatus.transferred or StudentStatus.left_housing
                ? st.ToString()
                : null;

        // الطلاب اللي بيتحسبوا في الإشغال: مش محذوفين ومربوطين بمبنى.
        // ⚠️ الحالة **مش شرط**: طالب حالته «تخرّج» ولسه شايل غرفة يبقى فيه خلل
        //    محتاج يبان على الخريطة لا يتشال منها. الإصلاح في StudentStatusService
        //    بيمنع تكرارها، والخريطة هي اللي بتوري الباقي من القديم.
        private IQueryable<Student> Occupants()
            => Scoped(_students.Query()).AsNoTracking().Where(s => !s.IsDeleted && s.BuildingId != null);

        public async Task<List<BuildingOccupancySummaryDto>> GetBuildingsAsync()
        {
            var scope = UnitOfWork.GetGenderScope();

            var buildings = await _buildings.Query().AsNoTracking()
                .Where(b => b.IsActive && (scope == null || b.Gender == scope))
                .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Code)
                .ToListAsync();

            // عدّ واحد لكل المباني بدل نداء لكل مبنى.
            var counts = await Occupants()
                .GroupBy(s => s.BuildingId!.Value)
                .Select(g => new { BuildingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BuildingId, x => x.Count);

            return buildings.Select(b => new BuildingOccupancySummaryDto
            {
                BuildingId = b.Id,
                Code = b.Code,
                Name = b.ArName,
                Gender = b.Gender?.ToString(),
                RoomCapacity = b.RoomCapacity,
                TotalPlaces = HousingStructure.RoomsPerBuilding * Math.Max(1, b.RoomCapacity),
                OccupiedPlaces = counts.TryGetValue(b.Id, out var c) ? c : 0
            }).ToList();
        }

        public async Task<BuildingOccupancyDto> GetBuildingAsync(int buildingId)
        {
            var scope = UnitOfWork.GetGenderScope();

            var building = await _buildings.Query().AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == buildingId)
                ?? throw UserFriendlyException.NotFound("المبنى غير موجود");

            // ⚠️ نفس قاعدة باقي الشاشات: مشرف قسم ما يفتحش مبنى القسم التاني
            //    حتى بالرابط المباشر. التصفية على الطلاب وحدها ماكانتش هتكفي -
            //    كان هيفتح المبنى ويلاقيه فاضي فيفتكره فاضي فعلًا.
            if (scope != null && building.Gender != scope)
                throw UserFriendlyException.Forbidden();

            var scheme = building.Numbering;

            var rows = await Occupants()
                .Where(s => s.BuildingId == buildingId)
                .Select(s => new
                {
                    s.Id,
                    s.student_id,
                    s.full_name,
                    s.floor_number,
                    s.apartment_number,
                    s.room_number,
                    s.student_status,
                    s.ad_status
                })
                .ToListAsync();

            var capacity = Math.Max(1, building.RoomCapacity);
            var maxCapacity = Math.Max(capacity, building.RoomCapacityMax);

            var dto = new BuildingOccupancyDto
            {
                BuildingId = building.Id,
                Code = building.Code,
                Name = building.ArName,
                Gender = building.Gender?.ToString(),
                RoomCapacity = capacity,
                RoomCapacityMax = maxCapacity,
                Numbering = scheme.ToString(),
                FloorCount = HousingStructure.FloorCount,
                ApartmentsPerFloor = HousingStructure.ApartmentsPerFloor,
                RoomsPerApartment = HousingStructure.RoomsPerApartment,
                TotalRooms = HousingStructure.RoomsPerBuilding
            };
            dto.TotalPlaces = dto.TotalRooms * capacity;

            // المفتاح تلاتة أجزاء: في الترقيم بالدور رقم الشقة بيتكرّر في كل دور.
            var grid = new Dictionary<(string floor, int apt, int room), OccupiedRoomDto>();

            foreach (var r in rows)
            {
                var floor = r.floor_number?.Trim() ?? "";
                var okApt = int.TryParse(r.apartment_number?.Trim(), out var apt);
                var okRoom = int.TryParse(r.room_number?.Trim(), out var room);

                string? bad = null;
                if (!okApt || !okRoom || string.IsNullOrWhiteSpace(floor)) bad = "incomplete";
                else if (!HousingStructure.IsValidFloor(floor)
                      || !HousingStructure.IsValidApartment(scheme, apt)
                      || !HousingStructure.IsValidRoom(scheme, apt, room)) bad = "outOfRange";
                else if (!HousingStructure.IsConsistent(scheme, floor, apt, room)) bad = "floorMismatch";

                if (bad != null)
                {
                    dto.Orphans.Add(new OrphanRowDto
                    {
                        Id = r.Id,
                        StudentNumber = r.student_id,
                        Name = r.full_name,
                        Floor = r.floor_number,
                        Apartment = r.apartment_number,
                        Room = r.room_number,
                        Reason = bad
                    });
                    continue;
                }

                var key = (floor, apt, room);
                if (!grid.TryGetValue(key, out var cell))
                {
                    cell = new OccupiedRoomDto { Floor = floor, Apartment = apt, Room = room };
                    grid[key] = cell;
                }

                cell.Occupants.Add(new OccupantDto
                {
                    Id = r.Id,
                    StudentNumber = r.student_id,
                    Name = r.full_name,
                    Status = DepartureStatus(r.student_status),
                    AdStatus = r.ad_status?.ToString()
                });
            }

            dto.Rooms = grid.Values
                .OrderBy(x => x.Floor).ThenBy(x => x.Apartment).ThenBy(x => x.Room)
                .ToList();

            dto.OccupiedRooms = dto.Rooms.Count;
            dto.OccupiedPlaces = dto.Rooms.Sum(x => x.Occupants.Count);
            dto.FullRooms = dto.Rooms.Count(x => x.Occupants.Count == capacity);
            dto.ExtraRooms = dto.Rooms.Count(x => x.Occupants.Count > capacity && x.Occupants.Count <= maxCapacity);
            dto.OverCapacityRooms = dto.Rooms.Count(x => x.Occupants.Count > maxCapacity);

            return dto;
        }
    }
}
