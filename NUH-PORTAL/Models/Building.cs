using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Models
{
    // قائمة المباني السكنية (lookup مُدار)
    public class Building
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;   // رقم المبنى: 65, 66...
        public string ArName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        // نوع المبنى: بنين (Male) أو بنات (Female) — نفس enum جنس الطالب. بيترشّح عليه المبنى وقت التسجيل.
        public Gender? Gender { get; set; }

        // ⚠️ السعة المعتمدة للغرفة في المبنى ده - العدد اللي السكن مصمَّم عليه.
        //    على المبنى لا على الغرفة عن قصد: البنية واحدة في كل المباني
        //    (٢٠ شقة × ٤ غرف)، واللي بيختلف هو السعة. عمود واحد بدل جدول
        //    ٨٠٠ غرفة مالوش أي معلومة غير رقم متكرّر.
        //
        //    القيم المؤكَّدة مع إدارة الإسكان:
        //      مباني ٦٦ و٦٨ و٦٩ و٧٠ (طلاب) → ٣
        //      مباني ٦٥ و٦٧ (طلاب)          → ٢
        //      كل مباني الطالبات             → ٢
        public int RoomCapacity { get; set; } = 2;

        // ⚠️ الحدّ اللي المشرف يقدر يوصّل له بالاستثناء - مش نفس السعة.
        //    إدارة الإسكان بتقول: مباني ٦٥ و٦٧ غرفها بنفرين، **لكن** المشرف
        //    يقدر يحطّ تالت؛ وسكن الطالبات بنتين، والمشرفة تقدر تزوّد تالتة.
        //    فالفرق بين الاتنين هو الفرق بين «المخطَّط» و«المسموح» - ومن غيره
        //    كنّا هنبقى قدام اختيارين وحشين: نمنع إجراء الإدارة بتعمله فعلًا،
        //    أو نفتح الغرفة بلا سقف فمحدش يعرف إمتى تبقى فيها مشكلة.
        //    الغرفة بين السعة والحدّ **بتبان مميَّزة** على الخريطة لا مخفية.
        public int RoomCapacityMax { get; set; } = 3;

        // ⚠️ أسلوب ترقيم الشقق والغرف - بيختلف بين سكن الطلاب وسكن الطالبات.
        //    الشرح الكامل في Models/Enums/HousingNumbering. عمود لا اشتقاق من
        //    الجنس: مبنى جديد بأسلوب مختلف بيتظبط بصفّه، من غير نشر ولا كود.
        public HousingNumbering Numbering { get; set; } = HousingNumbering.Continuous;

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
