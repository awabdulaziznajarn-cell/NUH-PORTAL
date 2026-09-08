using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // الشرح الكامل في IHousingPlacement.
    public class HousingPlacementService : IHousingPlacement
    {
        private readonly ILookupResolver _lookups;
        private readonly IHousingCapacityGuard _capacity;
        private readonly IRepository<HousingHistory> _history;
        private readonly IUnitOfWork _uow;

        public HousingPlacementService(ILookupResolver lookups, IHousingCapacityGuard capacity,
                                       IRepository<HousingHistory> history, IUnitOfWork uow)
        {
            _lookups = lookups;
            _capacity = capacity;
            _history = history;
            _uow = uow;
        }

        private static string N(string? v) => (v ?? "").Trim();

        public async Task<string?> ValidateAsync(string? buildingCode, string? floor, string? apartment,
                                                 string? room, int? excludeStudentId, bool allowExceptionSlot)
        {
            var code = N(buildingCode); var f = N(floor); var a = N(apartment); var r = N(room);

            if (code.Length == 0 || f.Length == 0 || a.Length == 0 || r.Length == 0)
                return "بيانات السكن غير مكتملة - المبنى والدور والشقة والغرفة كلها مطلوبة";

            var codeError = await _lookups.BuildingCodeErrorAsync(code);
            if (codeError != null) return codeError;

            // ⚠️ أسلوب الترقيم من صفّ المبنى نفسه: «شقة ٢ في الدور ٣» صحيحة في
            //    سكن الطالبات وغلط في سكن الطلاب.
            var scheme = await _lookups.NumberingForBuildingAsync(code);

            if (!HousingStructure.IsValidFloor(f))
                return $"رقم الدور غير صحيح - القيم المسموح بها: {string.Join("، ", HousingStructure.FloorCodes)}";

            if (!int.TryParse(a, out var apt) || !HousingStructure.ApartmentBelongsToFloor(scheme, f, a))
            {
                int.TryParse(f, out var fi);
                return $"رقم الشقة {a} لا ينتمي للدور {f} - الشقق في هذا الدور: "
                     + string.Join("، ", HousingStructure.ApartmentsFor(scheme, fi));
            }

            if (!int.TryParse(r, out var rm) || !HousingStructure.IsValidRoom(scheme, apt, rm))
                return $"رقم الغرفة {r} لا ينتمي للشقة {a} - الغرف في هذه الشقة: "
                     + string.Join("، ", HousingStructure.RoomsFor(scheme, apt));

            return await _capacity.CheckAsync(code, f, a, r, excludeStudentId, allowExceptionSlot);
        }

        public async Task ApplyAsync(Student student, string? buildingCode, string? floor,
                                     string? apartment, string? room, bool allowExceptionSlot,
                                     string? source = null, string? reason = null, int? transferId = null)
        {
            // ⚠️ الطالب نفسه مستثنى من عدّ السعة: تعديل بيانات ساكن أو تصحيح
            //    رقم دوره لا يُحسبان مرّتين في غرفته.
            var error = await ValidateAsync(buildingCode, floor, apartment, room,
                                            student.Id > 0 ? student.Id : null, allowExceptionSlot);
            if (error != null) throw new UserFriendlyException(error, 400);

            // الموضع السابق يُقرأ قبل الكتابة فوقه - وهو نصف السجل.
            var fromB = N(student.housing_building); var fromF = N(student.floor_number);
            var fromA = N(student.apartment_number); var fromR = N(student.room_number);

            student.housing_building = N(buildingCode);
            student.floor_number = N(floor);
            student.apartment_number = N(apartment);
            student.room_number = N(room);

            // ⚠️ المفتاح الأجنبي يُحلّ هنا لا على المستدعي: صفّ يحمل نصّ المبنى
            //    بلا BuildingId غير مرتبط بمبنى أصلًا، فلا يظهر في الخريطة ولا
            //    يُحسب في السعة - أي طالب ساكن لا يراه أحد.
            await _lookups.ApplyAsync(student);

            await LogAsync(student, fromB, fromF, fromA, fromR,
                           student.housing_building, student.floor_number,
                           student.apartment_number, student.room_number,
                           source, reason, transferId);
        }

        public async Task ClearAsync(Student student, string? reason, string? source)
        {
            var fromB = N(student.housing_building); var fromF = N(student.floor_number);
            var fromA = N(student.apartment_number); var fromR = N(student.room_number);

            student.housing_building = null;
            student.floor_number = null;
            student.apartment_number = null;
            student.room_number = null;
            student.BuildingId = null;

            await LogAsync(student, fromB, fromF, fromA, fromR, null, null, null, null,
                           source, reason, null);
        }

        // ====================================================================
        //  كتابة صفّ السجل - موضع واحد في النظام كلّه.
        //
        //  ⚠️ لا يُحفظ هنا: الصفّ يُضاف إلى السياق ويُحفظ مع حفظ الطالب نفسه
        //     في نفس SaveAsync - أي في معاملة واحدة. لو حُفظ على حدة لأمكن أن
        //     ينجح أحدهما ويفشل الآخر، فيتغيّر سكن بلا سجل، أو يُسجَّل نقل لم
        //     يقع. وهذه أسوأ حالة يقع فيها سجل.
        //
        //  ⚠️ ويُربط الطالب بالكائن لا بالرقم: الطالب الجديد رقمه صفر حتى
        //     يُحفظ، وEF هو الذي يملأ المفتاح من العلاقة عند الحفظ. وبغير ذلك
        //     كان أوّل سجل لكل طالب جديد يشير إلى الطالب رقم صفر.
        // ====================================================================
        private async Task LogAsync(Student student,
                                    string? fromB, string? fromF, string? fromA, string? fromR,
                                    string? toB, string? toF, string? toA, string? toR,
                                    string? source, string? reason, int? transferId)
        {
            var same = string.Equals(N(fromB), N(toB), StringComparison.OrdinalIgnoreCase)
                    && N(fromF) == N(toF) && N(fromA) == N(toA) && N(fromR) == N(toR);

            // لا شيء تغيّر - إعادة اعتماد الموضع نفسه ليست حركة، وتسجيلها
            // يملأ السجل بصفوف لا تقول شيئًا فيصير غير قابل للقراءة.
            if (same) return;

            var hadHousing = N(fromB).Length > 0 || N(fromR).Length > 0;
            var hasHousing = N(toB).Length > 0 || N(toR).Length > 0;

            var action = !hasHousing ? HousingHistoryKinds.Actions.Cleared
                       : !hadHousing ? HousingHistoryKinds.Actions.Assigned
                       : source == HousingHistoryKinds.Sources.Transfer
                            ? HousingHistoryKinds.Actions.Transferred
                            : HousingHistoryKinds.Actions.Edited;

            await _history.AddAsync(new HousingHistory
            {
                Student = student,
                StudentId = student.Id,
                StudentNumber = student.student_id,
                Action = action,
                Source = source,
                FromBuilding = fromB, FromFloor = fromF, FromApartment = fromA, FromRoom = fromR,
                ToBuilding = toB, ToFloor = toF, ToApartment = toA, ToRoom = toR,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                TransferId = transferId,
                CreatedBy = _uow.GetCurrentUserId(),
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
