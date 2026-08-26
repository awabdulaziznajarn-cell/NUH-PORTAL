using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Models
{
    // صف إشغال واحد = شخص واحد سكن في وحدة واحدة في فترة واحدة.
    //
    // ⚠️ ده الجدول اللي بيحلّ المشكلة الأصلية. قبله كان تغيير الساكن معناه
    //    الكتابة فوق description في الدومين — والاسم القديم بيتمسح للأبد،
    //    والدومين مابيسجّلش تاريخ تغيير الخصائص. فسؤال «مين كان ساكن في فيلا ٤٥
    //    السنة اللي فاتت؟» ما كانش ليه إجابة.
    //
    // ⚠️ الصف المقفول مايتعدلش أبدًا. تغيير الساكن = قفل الصف القديم بتاريخ
    //    + فتح صف جديد، مش تعديل الاسم في مكانه. لو عدّلنا في مكانه بنرجع
    //    لنفس مشكلة الدومين بالظبط، وده الغرض كله من الجدول.
    //
    // ⚠️ صف مفتوح واحد بس لكل وحدة — مضمونة بـ filtered unique index على
    //    (UnitId) WHERE EndDate IS NULL، مش بفحص في الكود. الفحص في الكود
    //    بيتنسى في مسار جديد أو بيتكسر مع طلبين في نفس اللحظة.
    public class FacultyOccupancy
    {
        public int Id { get; set; }
        public int UnitId { get; set; }

        // الاسم العربي — بيتكتب في description بتاع حساب الوحدة
        public string FullNameAr { get; set; } = string.Empty;

        // ⚠️ الاسم الإنجليزي — بيتكتب في displayName بتاع حساب الوحدة، ودي
        //    الخاصية السادسة اللي النظام بقى بيديرها. كانت مستثناة عن قصد
        //    على أساس إن displayName بيحمل اسم الوحدة نفسها (villa08)، لكن
        //    المعتمَد في دليل الجامعة إنها تحمل اسم الساكن بالإنجليزي - فبقت
        //    مصدر الاسم الإنجليزي في الاستيراد، ووجهته عند الحفظ.
        // ⚠️ اختياري لا إجباري: الوحدات المستوردة قبل ده مالهاش اسم إنجليزي
        //    مسجَّل، وجعله إجباريًّا كان هيمنع تعديل أي صفّ قديم لحد ما يتكتب
        //    الاسم - وde شرط على تصحيح حاجة تانية خالص.
        public string? FullNameEn { get; set; }

        // ⚠️ جنس الساكن — هنا مش على الوحدة، لأن الوحدات مختلطة والساكن هو اللي
        //    بيتغيّر. وهو كمان اللي بيحدّد الحساب يقعد في أنهي OU في الدومين
        //    (MALE / FEMALE)، فلما ساكن جديد يبقى جنسه مختلف عن اللي قبله
        //    النظام بيعرف إن الحساب محتاج ينتقل.
        public Gender? Gender { get; set; }
        // رقم الهوية/الإقامة — بيتكتب في employeeID
        public string? NationalId { get; set; }
        // الجوال — بيتكتب في mobile
        public string? Mobile { get; set; }
        // الكلية والقسم — بيتكتبوا في company و department
        public string? College { get; set; }
        public string? Department { get; set; }

        public DateTime StartDate { get; set; }
        // null = الساكن الحالي. أي قيمة هنا معناها الصف اتقفل ومابيتعدلش تاني.
        public DateTime? EndDate { get; set; }
        public OccupancyEndReason? EndReason { get; set; }
        public string? EndReasonNote { get; set; }

        // ⚠️ الطلب بييجي من نظام «إنجاز»، والتنفيذ بيتم هنا. رقم التذكرة هو
        //    الرابط الوحيد بين الاتنين — من غيره مافيش إجابة لسؤال «الإجراء ده
        //    جه منين وليه؟». إجباري في كل إجراء بيتعمل من الشاشة.
        public string? TicketNo { get; set; }

        // بوليسي الجامعة: أي حساب جديد لازم يعدّي على الأمن السيبراني الأول.
        // بنسجّل تاريخ الموافقة عشان يبقى جزء من الإثبات مش ذاكرة بشرية.
        public DateTime? CyberApprovedAt { get; set; }

        // مرفق الطلب اللي جه مع التذكرة — بيتخزّن مع صف الإشغال مش مع الطلب،
        // عشان يفضل مربوط بالساكن نفسه بعد ما التذكرة تتقفل في إنجاز.
        public string? AttachmentPath { get; set; }
        public string? OriginalFileName { get; set; }

        // ⚠️ الصف ده اتعمل من الاستيراد الأولي من الدومين؟ لو أيوه فتاريخ
        //    البداية تقديري مش حقيقي — الدومين مابيعرفش الساكن دخل إمتى.
        //    من غير العلامة دي هنبص بعد سنة على تاريخ ونفتكره موثّق وهو مش كده.
        public bool ImportedFromAd { get; set; }

        // آخر مرة اتأكّد فيها إن الساكن لسه موجود — لأن إدارة الإسكان مابتبلّغش
        // لما دكتور يمشي، فالصف ممكن يفضل مفتوح لواحد مشي من زمان.
        public DateTime? ConfirmedAt { get; set; }
        public int? ConfirmedBy { get; set; }

        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? ClosedBy { get; set; }

        public FacultyUnit? Unit { get; set; }
        public User? CreatedByUser { get; set; }
        public User? ClosedByUser { get; set; }
        public User? ConfirmedByUser { get; set; }
    }
}
