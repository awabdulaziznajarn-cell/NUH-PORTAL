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

        public async Task<RoomOccupancy?> GetRoomAsync(string? buildingCode, string? floor,
                                                       string? apartment, string? room, int? excludeStudentId)
        {
            var code = Norm(buildingCode); var f = Norm(floor); var a = Norm(apartment); var r = Norm(room);
            if (code.Length == 0 || f.Length == 0 || a.Length == 0 || r.Length == 0) return null;

            var building = (await _buildings.GetAllAsync())
                .FirstOrDefault(x => string.Equals(Norm(x.Code), code, StringComparison.OrdinalIgnoreCase));
            if (building == null) return null;

            var capacity = Math.Max(1, building.RoomCapacity);
            var maxCapacity = Math.Max(capacity, building.RoomCapacityMax);

            return new RoomOccupancy(capacity, maxCapacity, await CountAsync(building.Id, f, a, r, excludeStudentId));
        }

        public async Task<ApartmentOccupancy?> GetApartmentAsync(string? buildingCode, string? floor,
                                                                 string? apartment, int? excludeStudentId = null)
        {
            var code = Norm(buildingCode); var f = Norm(floor); var a = Norm(apartment);
            if (code.Length == 0 || f.Length == 0 || a.Length == 0) return null;

            var building = (await _buildings.GetAllAsync())
                .FirstOrDefault(x => string.Equals(Norm(x.Code), code, StringComparison.OrdinalIgnoreCase));
            if (building == null) return null;

            if (!int.TryParse(a, out var apt)) return null;

            // أرقام الغرف من نفس مصدر الشاشة: أسلوب ترقيم المبنى.
            var rooms = HousingStructure.RoomsFor(building.Numbering, apt);
            if (rooms.Count == 0) return null;

            // ⚠️ استعلام واحد بـ GroupBy لا استعلام لكل غرفة: أربع رحلات لقاعدة
            //    البيانات عشان أربعة أرقام مالهاش لازمة، والقائمة بتتفتح كل ثانية.
            var q = _students.Query().AsNoTracking()
                .Where(st => !st.IsDeleted
                             && st.BuildingId == building.Id
                             && st.floor_number == f
                             && st.apartment_number == a
                             && st.room_number != null);

            // ⚠️ الاستثناء نفسه الوارد في CheckAsync حرفيًا: الطالب محلّ
            //    التسكين لا يُعدّ على نفسه. وبغير هذا السطر يرى المشرف الذي
            //    يراجع طالبًا مقيمًا في الشقة نفسها غرفته أضيق بموضع، ثم
            //    يقبلها الحفظ - رقمان مختلفان لغرفة واحدة في شاشة واحدة.
            if (excludeStudentId is int ex && ex > 0)
                q = q.Where(st => st.Id != ex);

            var counts = await q
                .GroupBy(st => st.room_number!)
                .Select(g => new { Room = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Room, x => x.Count);

            var capacity = Math.Max(1, building.RoomCapacity);
            var list = rooms.Select(r => new RoomCount(r,
                            counts.TryGetValue(r.ToString(), out var n) ? n : 0)).ToList();

            return new ApartmentOccupancy(capacity, Math.Max(capacity, building.RoomCapacityMax), list);
        }

        // ⚠️ العدّ في مكان واحد: الفحص والعرض لازم يقروا نفس الرقم. لو العرض
        //    عدّ بطريقة والفحص بطريقة، المشرف بيشوف «فيها مكان» ويترفض عند
        //    الحفظ - وساعتها بيبطّل يثق في السطر اللي فوق الخانة.
        //    وبلا Scoped: التقسيم بيخفي بيانات عن العرض، وما ينفعش يخفي مكانًا
        //    مشغولًا عن الحساب.
        private async Task<int> CountAsync(int buildingId, string floor, string apartment,
                                           string room, int? excludeStudentId)
        {
            var query = _students.Query().AsNoTracking()
                .Where(s => !s.IsDeleted
                            && s.BuildingId == buildingId
                            && s.floor_number == floor
                            && s.apartment_number == apartment
                            && s.room_number == room);

            if (excludeStudentId is int id && id > 0)
                query = query.Where(s => s.Id != id);

            return await query.CountAsync();
        }

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

            var taken = await CountAsync(building.Id, f, apt, rm, excludeStudentId)
                      + Math.Max(0, pendingInSameRoom);

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
