using NUH_PORTAL.DTOs.Registration;

namespace NUH_PORTAL.DTOs.Students
{
    // ========================================================================
    //  ملف الطالب المجمّع - لشاشة الأمن السيبراني.
    //
    //  ⚠️ الشاشة دي بتجمع في رد واحد ما كان مفرّقًا على أربع شاشات: بيانات
    //     الطالب، طلباته، تعهّده الموقّع، وحساب شبكته. والسبب إن التحقيق
    //     بيحتاجهم مع بعض في ورقة واحدة - مش إن الموظف يلفّ على الشاشات
    //     ويجمّعهم بإيده وهو عارف إنه ممكن ينسى واحدة.
    //
    //  ⚠️ ومفيش هنا أي بيان جديد: كل حاجة موجودة أصلًا في النظام، والجديد هو
    //     التجميع. ولذلك الشاشة ورا صلاحية مستقلة وكل فتح ليها بيتسجّل.
    // ========================================================================
    public class StudentFileDto
    {
        public StudentDto? Student { get; set; }

        // ⚠️ السجل المحذوف بيظهر معلَّمًا لا مخفيًّا: التحقيق غالبًا بيبدأ بعد
        //    ما يكون الطالب اتشال من السكن، وإخفاء سجله بيخلّي الأداة عديمة
        //    الفايدة في أكتر حالة محتاجينها فيها.
        public bool IsDeleted { get; set; }

        public List<StudentFileRequestDto> Requests { get; set; } = new();

        // الطلب المعروض حاليًا (الأحدث افتراضيًا، أو اللي اختاره الموظف).
        public int? SelectedRequestId { get; set; }
        public string? SelectedRequestNumber { get; set; }

        public PledgeRecordDto? Pledge { get; set; }

        public StudentFileAdDto? Ad { get; set; }

        // ⚠️ الدليل النشط ممكن يكون مش متاح لحظة الفتح. الملف بيرجع كامل
        //    والخانة دي بتقول للشاشة تعرض «تعذّر الوصول للدليل» بدل ما تعرض
        //    فراغ يتقري كأن الطالب مالوش حساب.
        public bool AdUnavailable { get; set; }

        // ====================================================================
        //  نتيجة التحقّق - بتتملى لمّا يكون البحث برمز مطبوع على وثيقة.
        //
        //  ⚠️ الملف واحد سواء وصلنا له برقم جامعي أو برمز؛ اللي بيفرق إن
        //     الوصول بالرمز بيجاوب سؤالًا زيادة: «الورقة اللي في إيدي صادرة
        //     عن النظام؟». الشاشة بتعرض الإجابة دي فوق الملف، وبتفضل null في
        //     البحث العادي فمفيش شريط بيظهر بلا سبب.
        // ====================================================================
        public StudentFileVerifiedDto? Verified { get; set; }
    }

    public class StudentFileVerifiedDto
    {
        public string? Code { get; set; }
        public string? RequestNumber { get; set; }
        public DateTime AcceptedAt { get; set; }
        public string? PolicyVersion { get; set; }
        public int TermsCount { get; set; }
    }

    public class StudentFileRequestDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? RequestType { get; set; }
        public string? Status { get; set; }
        public DateTime SubmittedAt { get; set; }

        // هل للطلب ده تعهّد موثّق؟ عشان الموظف يعرف قبل ما يفتحه.
        public bool HasPledge { get; set; }
    }

    public class StudentFileAdDto
    {
        public string? Username { get; set; }
        public bool Enabled { get; set; }

        // ⚠️ آخر دخول للشبكة من الدليل مباشرة لا من قاعدة بياناتنا: القيمة دي
        //    غالبًا أول حاجة التحقيق بيبدأ منها، وقيمة قديمة محفوظة عندنا
        //    أسوأ من مفيش قيمة.
        public DateTime? LastLogonAt { get; set; }
    }
}
