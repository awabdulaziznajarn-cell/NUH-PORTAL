using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Models
{
    public class Request
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

        // ⚠️ جنس الطالب متكرّر هنا عن قصد، مش نُسخ زيادة بلا داعي.
        //    طلب التسجيل الذاتي بيتقدّم و student_id فيه صفر لحد ما يتعتمد —
        //    بيانات الطالب ساعتها نص JSON في RegistrationData. والـ JSON مايتفلترش
        //    في SQL. فمن غير العمود ده مافيش طريقة نوجّه الطلب المعلّق للمشرف
        //    المسؤول عن قسمه، وهو بالظبط الطلب اللي محتاج التوجيه.
        public Gender? StudentGender { get; set; }

        // ⚠️ الخانات اللي المراجع طلب من الطالب يصلّحها — مفاتيحها مفصولة بفاصلة.
        //    فاضي = كل الخانات مفتوحة (ده حال الطلبات المرجّعة قبل الميزة دي).
        //    بيتفضّى مع كل إعادة تقديم: الجولة الجاية ليها طلبها هي.
        public string? InfoFields { get; set; }

        // ====================================================================
        //  ختم النسخة — الحارس ضد مراجعَين في نفس اللحظة.
        //
        //  ⚠️ المراجعة كانت: نقرا الطلب، نتأكد إن مرحلته تسمح، نعدّل، نحفظ.
        //     ولو اتنين فتحوا نفس الطلب في نفس الوقت — واحد يعتمد وواحد يرفض —
        //     الاتنين بيقروا نفس المرحلة، والاتنين بيعدّوا الفحص، والاتنين
        //     بينجحوا. آخر واحد يكتب هو اللي بيفضل، وسجل المراحل بيسجّل
        //     الإجراءين، والطالب بيستلم إشعارين متناقضين.
        //
        //  ⚠️ العمود ده SQL Server هو اللي بيملاه ويغيّره مع كل تعديل — إحنا
        //     ما بنكتبش فيه. و EF بيحطّه في شرط الـ UPDATE، فلو اتغيّر بين
        //     القراءة والحفظ بيرمي DbUpdateConcurrencyException بدل ما يدهس
        //     تعديل التاني. الترجمة لرسالة مفهومة في ExceptionHandlingMiddleware.
        // ====================================================================
        public byte[]? RowVersion { get; set; }

        public Student? Student { get; set; }
    }
}
