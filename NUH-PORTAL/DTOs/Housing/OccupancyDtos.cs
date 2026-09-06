namespace NUH_PORTAL.DTOs.Housing
{
    // ========================================================================
    //  خريطة إشغال المباني - نتيجة الاستعلام.
    //
    //  ⚠️ الخريطة **مشتقّة** لا مخزَّنة: مافيش جدول غرف، والإشغال بيتحسب في
    //     كل نداء من صفوف الطلاب نفسها. يعني مستحيل تفرق عن الحقيقة، ومفيش
    //     حاجة تحتاج «إعادة حساب» لو حد اتنقل أو غادر.
    // ========================================================================

    // ساكن واحد في غرفة.
    public class OccupantDto
    {
        public int Id { get; set; }
        public string? StudentNumber { get; set; }
        public string? Name { get; set; }
        // حالة الطالب زي ما هي متخزّنة (graduated / left_housing / ...) -
        // فاضية يعني ساكن عادي. بتظهر كتنبيه على المربّع: صفّ لسه شايل غرفة
        // ومعاه حالة مغادرة يبقى فيه خلل في البيانات، والخريطة بتوريه بدل
        // ما تخبّيه.
        public string? Status { get; set; }
        public string? AdStatus { get; set; }
    }

    // غرفة فيها ساكن واحد على الأقل. الغرف الفاضية **مش بتترسل**: العميل
    // بيبني الشبكة كاملة من NuhHousingStructure ويملاها باللي وصله - فالردّ
    // بيفضل صغير مهما كبر السكن.
    //
    // ⚠️ والمفتاح تلاتة أجزاء (دور + شقة + غرفة) لا اتنين: في سكن الطالبات
    //    الترقيم بيبدأ من أول كل دور، فـ«شقة ٢ غرفة ٣» موجودة خمس مرات في
    //    المبنى - واحدة في كل دور.
    public class OccupiedRoomDto
    {
        public string Floor { get; set; } = "";
        public int Apartment { get; set; }
        public int Room { get; set; }
        public List<OccupantDto> Occupants { get; set; } = new();
    }

    // صفّ بياناته خارج بنية المبنى (شقة ٩٩، أو شقة مش في دورها، أو رقم غرفة
    // مش من غرف الشقة).
    // ⚠️ بيترجع في قايمة مستقلة عن قصد: لو اتحطّ في الشبكة هيختفي، ولو
    //    اتشال خالص هيبقى طالب ساكن ومحدش شايفه.
    public class OrphanRowDto
    {
        public int Id { get; set; }
        public string? StudentNumber { get; set; }
        public string? Name { get; set; }
        public string? Floor { get; set; }
        public string? Apartment { get; set; }
        public string? Room { get; set; }
        public string? Reason { get; set; }   // outOfRange | floorMismatch | incomplete
    }

    public class BuildingOccupancyDto
    {
        public int BuildingId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Gender { get; set; }

        // السعة المعتمدة، والحدّ اللي المشرف يقدر يوصّل له بالاستثناء.
        public int RoomCapacity { get; set; }
        public int RoomCapacityMax { get; set; }
        // "Continuous" أو "PerFloor" - العميل بيبني الترقيم منها.
        public string Numbering { get; set; } = "Continuous";

        public int FloorCount { get; set; }
        public int ApartmentsPerFloor { get; set; }
        public int RoomsPerApartment { get; set; }

        public int TotalRooms { get; set; }
        public int TotalPlaces { get; set; }        // الأماكن المعتمدة = الغرف × السعة
        public int OccupiedPlaces { get; set; }
        public int OccupiedRooms { get; set; }
        public int FullRooms { get; set; }          // وصلت السعة بالظبط
        public int ExtraRooms { get; set; }         // فوق السعة وجوّه الحدّ المسموح
        public int OverCapacityRooms { get; set; }  // تعدّت الحدّ - خلل بيانات

        public List<OccupiedRoomDto> Rooms { get; set; } = new();
        public List<OrphanRowDto> Orphans { get; set; } = new();
    }

    // سطر في شريط اختيار المبنى - فيه العدّاد عشان المستخدم يشوف الضغط قبل
    // ما يفتح المبنى.
    public class BuildingOccupancySummaryDto
    {
        public int BuildingId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Gender { get; set; }
        public int RoomCapacity { get; set; }
        public int TotalPlaces { get; set; }
        public int OccupiedPlaces { get; set; }
    }
}
