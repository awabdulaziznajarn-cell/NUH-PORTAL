namespace NUH_PORTAL.Models
{
    // ========================================================================
    //  سجل حركة التسكين - أثر دائم لكل تغيير في سكن الطالب.
    //
    //  ⚠️ سبب وجوده: سكن الطالب أربعة حقول في صفّه، وكل تغيير يكتب فوق القديم
    //     فيختفي بلا أثر. الطالب الذي انتقل من غرفة إلى غرفة، والطالب الذي
    //     أُخلي سكنه عند التخرّج - كأنه لم يسكن قطّ. فسؤال «من سكن في هذه
    //     الغرفة» لا إجابة له في صفوف الطلاب وحدها.
    //
    //  ⚠️ وهو لا يُغني عن HousingTransfer ولا يُغنيه: ذاك يحفظ نقل المشرف
    //     بسببه ومستنده، وهذا يحفظ **كل** حركة - التسكين الأول عند اعتماد
    //     الطلب، والتصحيح من شاشة الطلاب، والرفع الجماعي، والإخلاء عند تغيير
    //     الحالة. وحين تكون الحركة نقلًا يشير الصفّ إلى رقم النقل ولا ينسخ
    //     بياناته: المستند يبقى في مكانه الواحد.
    //
    //  ⚠️ ولا يُحذف صفّ منه أبدًا - ولهذا الرقم الجامعي منسوخ نصًّا داخله:
    //     لو حُذف صفّ الطالب يبقى السجل مقروءًا. سجل يُحذف مع صاحبه ليس سجلًا.
    // ========================================================================
    public class HousingHistory
    {
        public int Id { get; set; }

        public int StudentId { get; set; }

        // منسوخ نصًّا عن قصد - لا يُقرأ من صفّ الطالب وقت العرض.
        public string? StudentNumber { get; set; }

        // القيم في Core/HousingHistoryKinds.Actions
        public string Action { get; set; } = "";

        // من أين جاء التغيير - القيم في Core/HousingHistoryKinds.Sources
        public string? Source { get; set; }

        // الموضع السابق - فارغ في التسكين الأول.
        public string? FromBuilding { get; set; }
        public string? FromFloor { get; set; }
        public string? FromApartment { get; set; }
        public string? FromRoom { get; set; }

        // الموضع الجديد - فارغ في الإخلاء.
        public string? ToBuilding { get; set; }
        public string? ToFloor { get; set; }
        public string? ToApartment { get; set; }
        public string? ToRoom { get; set; }

        // سبب النقل، أو الحالة التي أدّت إلى الإخلاء (تخرّج، فصل، ترك السكن…)
        public string? Reason { get; set; }

        // رابط إلى صفّ النقل حين تكون الحركة نقلًا - المستند والسبب التفصيلي هناك.
        public int? TransferId { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public Student? Student { get; set; }
        public User? CreatedByUser { get; set; }
        public HousingTransfer? Transfer { get; set; }
    }
}
