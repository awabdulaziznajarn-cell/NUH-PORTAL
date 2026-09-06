using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // الشرح الكامل في IHousingCapacityGuard.
    public class HousingCapacityService : IHousingCapacityGuard
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Building> _buildings;

        public HousingCapacityService(IRepository<Student> students, IRepository<Building> buildings)
        {
            _students = students;
            _buildings = buildings;
        }

        private static string Norm(string? v) => (v ?? "").Trim();

        public async Task<string?> CheckAsync(string? buildingCode, string? floor, string? apartment, string? room,
                                              int? excludeStudentId, bool allowExceptionSlot, int pendingInSameRoom = 0)
        {
            var code = Norm(buildingCode);
            var f = Norm(floor);
            var apt = Norm(apartment);
            var rm = Norm(room);

            // ⚠️ الخانات الفاضية مش شغل الحارس ده: «رقم الغرفة مطلوب» رسالة
            //    بتخصّ الخانة نفسها وبتتقال في مكانها. الحارس بيتكلم عن السعة
            //    وبس، ولو اتكلم عن حاجة تانية بقى رسالتين لنفس الخطأ.
            if (code.Length == 0 || f.Length == 0 || apt.Length == 0 || rm.Length == 0) return null;

            var building = (await _buildings.GetAllAsync())
                .FirstOrDefault(b => string.Equals(Norm(b.Code), code, StringComparison.OrdinalIgnoreCase));

            // مبنى مش موجود: رسالته من ILookupResolver.BuildingCodeErrorAsync،
            // ومش من هنا. نفس السبب اللي فوق.
            if (building == null) return null;

            var capacity = Math.Max(1, building.RoomCapacity);
            var maxCapacity = Math.Max(capacity, building.RoomCapacityMax);
            var limit = allowExceptionSlot ? maxCapacity : capacity;

            // ⚠️ بلا Scoped: العدّ لازم يشوف كل الساكنين في الغرفة مهما كان قسمهم.
            //    التقسيم بيخفي بيانات عن **العرض**، وما ينفعش يخفي مكانًا مشغولًا
            //    عن **الحساب**.
            var query = _students.Query().AsNoTracking()
                .Where(s => !s.IsDeleted
                            && s.BuildingId == building.Id
                            && s.floor_number == f
                            && s.apartment_number == apt
                            && s.room_number == rm);

            if (excludeStudentId is int id && id > 0)
                query = query.Where(s => s.Id != id);

            var taken = await query.CountAsync() + Math.Max(0, pendingInSameRoom);

            if (taken < limit) return null;

            var where = $"مبنى {code} - {FloorName(f)} - شقة {apt} - غرفة {rm}";

            // ⚠️ رسالتان مختلفتان عن قصد. الطالب اللي وصل للسعة المعتمدة لسه
            //    فيه احتمال يتسكّن باستثناء من الإدارة، فالرسالة بتوجّهه لهم.
            //    واللي وصل للحدّ الأقصى مافيش استثناء بعده، فالرسالة بتقول
            //    اختر غيرها - وتوجيهه للإدارة كان هيبقى وعدًا كاذبًا.
            if (!allowExceptionSlot && taken < maxCapacity)
                return $"الغرفة مكتملة: {where} - سعتها {capacity} ومسجَّل فيها {taken}. "
                     + "اختر غرفة أخرى، أو راجع إدارة الإسكان إن كان لديك تسكين خاص فيها.";

            return $"الغرفة وصلت الحدّ الأقصى المسموح: {where} - الحدّ {maxCapacity} ومسجَّل فيها {taken}. "
                 + "اختر غرفة أخرى.";
        }

        public async Task EnsureAsync(string? buildingCode, string? floor, string? apartment, string? room,
                                      int? excludeStudentId, bool allowExceptionSlot, int pendingInSameRoom = 0)
        {
            var error = await CheckAsync(buildingCode, floor, apartment, room, excludeStudentId, allowExceptionSlot, pendingInSameRoom);
            if (error != null) throw new UserFriendlyException(error, 400);
        }

        // "0" بتتعرض «الأرضي» - نفس ما تعرضه الشاشات.
        private static string FloorName(string floor)
            => floor == "0" ? "الدور الأرضي" : $"الدور {floor}";
    }
}
