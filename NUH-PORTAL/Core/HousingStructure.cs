using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Core
{
    // ========================================================================
    //  بنية السكن الجامعي - المصدر الوحيد للأدوار والشقق والغرف.
    //
    //  ⚠️ القاعدة كانت مكتوبة في wwwroot/js/housing-fields.js وبس - يعني في
    //     المتصفح وحده. الخادم مكانش يعرف إن المبنى فيه ٢٠ شقة، فأي طلب
    //     بيوصله بشقة ٩٩ كان بيتقبل: الحارس الوحيد كان القائمة المنسدلة،
    //     وده تحقّق شكلي أي حد يعدّيه.
    //
    //     دلوقتي القاعدة هنا، والمتصفح بياخدها **متولّدة** من نفس الملف عبر
    //     /js/nuh-housing.js - نفس نمط nuh-id.js و nuh-pledge.js.
    //
    //  البنية (مؤكَّدة مع إدارة الإسكان):
    //     كل مبنى = الأرضي + ٤ أدوار، كل دور ٤ شقق، كل شقة ٤ غرف.
    //
    //  ⚠️ والترقيم **مش واحد**: سكن الطلاب متّصل عبر المبنى (شقق ١-٢٠ وغرف
    //     ١-٨٠)، وسكن الطالبات بيبدأ من أول في كل دور (شقق ١-٤ وغرف ١-٤).
    //     الفرق ده في السكن نفسه لا في العرض، ومصدره عمود Numbering على
    //     المبنى - فمبنى جديد بياخد أسلوبه من صفّه لا من كود مكتوب.
    //     الشرح الكامل في Models/Enums/HousingNumbering.
    //
    //  ⚠️ والسعة **مش هنا** كمان: هي RoomCapacity و RoomCapacityMax على جدول
    //     Buildings، لأنها بتختلف من مبنى للتاني حتى جوّه نفس النوع (٦٥ و٦٧
    //     غرفهم بنفرين والباقي بتلاتة) - والبنية دي واحدة في الكل.
    // ========================================================================
    public static class HousingStructure
    {
        public const int FloorCount = 5;            // الأرضي + ٤
        public const int ApartmentsPerFloor = 4;
        public const int RoomsPerApartment = 4;

        // "0" = الأرضي. بيتخزّن كود رقمي عشان الترتيب والفرز يفضلوا رقميين،
        // والعرض بيترجمه (floorName في الواجهة).
        public static readonly string[] FloorCodes = { "0", "1", "2", "3", "4" };

        public static int ApartmentsPerBuilding => FloorCount * ApartmentsPerFloor;   // ٢٠
        public static int RoomsPerBuilding => ApartmentsPerBuilding * RoomsPerApartment; // ٨٠

        // الدور → أرقام شققه.
        //   متّصل : الدور ٣ → ١٣، ١٤، ١٥، ١٦
        //   بالدور: أي دور → ١، ٢، ٣، ٤
        public static IReadOnlyList<int> ApartmentsFor(HousingNumbering scheme, int floor)
        {
            if (floor < 0 || floor >= FloorCount) return Array.Empty<int>();
            var start = scheme == HousingNumbering.Continuous ? floor * ApartmentsPerFloor + 1 : 1;
            return Enumerable.Range(start, ApartmentsPerFloor).ToList();
        }

        // الشقة → أرقام غرفها.
        //   متّصل : الشقة ٥ → ١٧، ١٨، ١٩، ٢٠
        //   بالدور: أي شقة → ١، ٢، ٣، ٤
        public static IReadOnlyList<int> RoomsFor(HousingNumbering scheme, int apartment)
        {
            if (!IsValidApartment(scheme, apartment)) return Array.Empty<int>();
            var start = scheme == HousingNumbering.Continuous ? (apartment - 1) * RoomsPerApartment + 1 : 1;
            return Enumerable.Range(start, RoomsPerApartment).ToList();
        }

        // رقم الشقة → الدور. في الترقيم بالدور الرقم مابيقولش الدور أصلًا
        // (شقة ٢ موجودة في الخمس أدوار)، فبترجع ‎-1‎ ولازم الدور يتقري من صفّه.
        public static int FloorOf(HousingNumbering scheme, int apartment)
        {
            if (scheme != HousingNumbering.Continuous) return -1;
            return (apartment < 1 || apartment > ApartmentsPerBuilding)
                ? -1
                : (apartment - 1) / ApartmentsPerFloor;
        }

        public static bool IsValidFloor(string? floor)
            => floor != null && Array.IndexOf(FloorCodes, floor.Trim()) >= 0;

        public static bool IsValidApartment(HousingNumbering scheme, int apartment)
            => scheme == HousingNumbering.Continuous
                ? apartment >= 1 && apartment <= ApartmentsPerBuilding
                : apartment >= 1 && apartment <= ApartmentsPerFloor;

        public static bool IsValidRoom(HousingNumbering scheme, int apartment, int room)
        {
            var rooms = RoomsFor(scheme, apartment);
            return rooms.Count > 0 && room >= rooms[0] && room <= rooms[rooms.Count - 1];
        }

        // ⚠️ التحقّق الكامل: الشقة لازم تكون **في** الدور المُرسَل والغرفة **في**
        //    الشقة. ده اللي مكانش موجود خالص - الشاشة بتمنعه والخادم مكانش بيشوفه.
        public static bool IsConsistent(HousingNumbering scheme, string? floor, int apartment, int room)
        {
            if (!IsValidFloor(floor)) return false;
            if (!IsValidApartment(scheme, apartment)) return false;
            if (!IsValidRoom(scheme, apartment, room)) return false;
            if (scheme != HousingNumbering.Continuous) return true;   // الدور مستقلّ عن الرقم
            return FloorOf(scheme, apartment) == int.Parse(floor!.Trim());
        }

        // نفس الفحص بصيغته النصّية - الشاشة والإكسل بيبعتوا نصًّا لا رقمًا.
        // ⚠️ كان مكتوبًا بإيده مرتين (SupervisorHousingTransferService و
        //    BulkRegistrationService) بنفس الثابت ٤ مكرّر في التلاتة.
        public static bool ApartmentBelongsToFloor(HousingNumbering scheme, string? floor, string? apartment)
        {
            if (!int.TryParse(floor?.Trim(), out var f) || f < 0 || f >= FloorCount) return false;
            if (!int.TryParse(apartment?.Trim(), out var a)) return false;
            if (!IsValidApartment(scheme, a)) return false;
            return scheme != HousingNumbering.Continuous || FloorOf(scheme, a) == f;
        }

        private static string? _js;
        public static string ToJavaScript() => _js ??= BuildJavaScript();

        private static string BuildJavaScript() =>
$$"""
// ============================================================================
//  nuh-housing.js - بنية السكن: الأدوار والشقق والغرف وأسلوب ترقيمها.
//
//  ⚠️ الملف ده **متولَّد** من Core/HousingStructure.cs. ماتعدّلش فيه - أي
//     تعديل هنا بيروح مع أول طلب. البنية تتغيّر في ملف الـ C# وبس، فتوصل
//     للمتصفح وللتحقّق على الخادم في نفس اللحظة.
//
//  ⚠️ والاسم NuhHousingStructure لا NuhHousing: التاني اسم غلاف الخانات في
//     js/housing-fields.js، وكان بيدهس الملف ده لما الاتنين يتحمّلوا مع بعض.
// ============================================================================
var NuhHousingStructure = (function () {
  'use strict';

  var FLOOR_COUNT          = {{FloorCount}};
  var APARTMENTS_PER_FLOOR = {{ApartmentsPerFloor}};
  var ROOMS_PER_APARTMENT  = {{RoomsPerApartment}};
  var FLOOR_CODES          = {{System.Text.Json.JsonSerializer.Serialize(FloorCodes)}};

  var CONTINUOUS = 'Continuous';   // سكن الطلاب - الترقيم متّصل عبر المبنى
  var PER_FLOOR  = 'PerFloor';     // سكن الطالبات - الترقيم بيبدأ من أول كل دور

  // أي قيمة مش معروفة بتتعامل كـ Continuous: هو الترقيم الأوسع، فالتحقّق
  // بيفضل مش أضيق من اللازم لو صفّ مبنى قديم لسه بلا أسلوب.
  function norm(scheme) { return scheme === PER_FLOOR ? PER_FLOOR : CONTINUOUS; }

  function range(start, n) {
    var out = [];
    for (var i = 0; i < n; i++) out.push(String(start + i));
    return out;
  }

  // الدور → أرقام الشقق.
  function apartmentsFor(scheme, floor) {
    var f = parseInt(floor, 10);
    if (isNaN(f) || f < 0 || f >= FLOOR_COUNT) return [];
    return range(norm(scheme) === CONTINUOUS ? f * APARTMENTS_PER_FLOOR + 1 : 1, APARTMENTS_PER_FLOOR);
  }

  // الشقة → أرقام غرفها.
  function roomsFor(scheme, apartment) {
    var a = parseInt(apartment, 10);
    if (isNaN(a) || a < 1) return [];
    if (norm(scheme) !== CONTINUOUS) return a > APARTMENTS_PER_FLOOR ? [] : range(1, ROOMS_PER_APARTMENT);
    if (a > FLOOR_COUNT * APARTMENTS_PER_FLOOR) return [];
    return range((a - 1) * ROOMS_PER_APARTMENT + 1, ROOMS_PER_APARTMENT);
  }

  // رقم الشقة → الدور. ‎-1‎ في الترقيم بالدور: الرقم مابيقولش الدور أصلًا.
  function floorOf(scheme, apartment) {
    if (norm(scheme) !== CONTINUOUS) return -1;
    var a = parseInt(apartment, 10);
    if (isNaN(a) || a < 1 || a > FLOOR_COUNT * APARTMENTS_PER_FLOOR) return -1;
    return Math.floor((a - 1) / APARTMENTS_PER_FLOOR);
  }

  return {
    CONTINUOUS: CONTINUOUS,
    PER_FLOOR: PER_FLOOR,
    FLOOR_COUNT: FLOOR_COUNT,
    APARTMENTS_PER_FLOOR: APARTMENTS_PER_FLOOR,
    ROOMS_PER_APARTMENT: ROOMS_PER_APARTMENT,
    APARTMENTS_PER_BUILDING: FLOOR_COUNT * APARTMENTS_PER_FLOOR,
    ROOMS_PER_BUILDING: FLOOR_COUNT * APARTMENTS_PER_FLOOR * ROOMS_PER_APARTMENT,
    FLOOR_CODES: FLOOR_CODES,
    apartmentsFor: apartmentsFor,
    roomsFor: roomsFor,
    floorOf: floorOf
  };
})();
""";
    }
}
