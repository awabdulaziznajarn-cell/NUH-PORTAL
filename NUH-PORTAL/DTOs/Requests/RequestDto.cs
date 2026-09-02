using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.DTOs.Requests
{
    // نفس شكل الـ Request entity (اللي كان بيترجع زي ما هو) للحفاظ على الـ JSON
    public class RequestDto
    {
        public int Id { get; set; }
        public RequestType? RequestType { get; set; }
        public int StudentId { get; set; }
        public int? SubmittedBy { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public string? RequestedByRole { get; set; }
        public int? HousingReviewedBy { get; set; }
        public DateTime? HousingReviewedAt { get; set; }
        public string? HousingNotes { get; set; }
        public int? CyberReviewedBy { get; set; }
        public DateTime? CyberReviewedAt { get; set; }
        public string? CyberNotes { get; set; }
        public DateTime? ReadyForProvisioningAt { get; set; }
        public int? ReadyForProvisioningBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public int? BulkRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public string? RegistrationData { get; set; }
        public StudentDto? Student { get; set; }

        // ====================================================================
        //  متى دخل الطلب مرحلته الحالية.
        //
        //  ⚠️ ليه الحقل ده موجود: لوحة «الطلبات الأطول انتظارًا» كانت بتعرض
        //     عمر الطلب من يوم التقديم. الرقم ده مؤشّر إداري، لكنه مش تنبيه:
        //     طلب عمره ٦ أيام قضى ٥ منها عند الأمن السيبراني ووصل الإسكان
        //     امبارح كان بيظهر لموظف الإسكان بالأحمر «٦ أيام» وهو ما قصّرش.
        //     وأول ما ده يحصل كام مرة، الموظف بيبطّل يبصّ للرقم خالص.
        //
        //  ⚠️ وبيتحسب من الأختام الموجودة أصلًا في الطلب لا من جدول جديد:
        //     أحدث ختم غير فاضي هو لحظة آخر انتقال. الترتيب من الأحدث مرحلةً
        //     للأقدم، فأول ختم موجود هو الصح - والتقديم هو الأساس اللي
        //     مايكونش فاضي أبدًا.
        //
        //  ⚠️ و ReviewedAt خارج السلسلة عن قصد: هو ختم «آخر مراجعة» أيًّا كانت،
        //     فمش مربوط بمرحلة بعينها، ودخوله السلسلة كان هيدّي ترتيبًا غير
        //     متّسق مع أختام المراحل المرتّبة زمنيًّا فوقه.
        //
        //  ⚠️ محسوب هنا لا في الواجهة: نفس القيمة بيترتّب بيها الاستعلام على
        //     الخادم (sortBy=stagesince)، ونسخة تانية في الجافاسكربت كانت
        //     هتخلّي الترتيب والشارة يقولوا حاجتين مختلفتين.
        // ====================================================================
        public DateTime StageSince =>
            ReadyForProvisioningAt ?? CyberReviewedAt ?? HousingReviewedAt ?? SubmittedAt;
    }
}
