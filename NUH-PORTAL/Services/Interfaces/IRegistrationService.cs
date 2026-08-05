using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // أي حقل سبّب التعارض. الرسالة لازم تقول للطالب البيانات المكرّرة بالظبط:
    // «رقم الهوية مرتبط بطلب رقم كذا» تختلف تمامًا عن «لديك طلب قائم» —
    // الأولى تُرشد لتصحيح إدخال، والثانية تُرشد لمتابعة طلب.
    public enum DuplicateField { StudentId, NationalId, Mobile }

    public sealed class DuplicateMatch
    {
        public DuplicateField Field { get; set; }
        public string RequestNumber { get; set; } = string.Empty;

        public string FieldLabel => Field switch
        {
            DuplicateField.StudentId  => "الرقم الجامعي",
            DuplicateField.NationalId => "رقم الهوية",
            _                         => "رقم الجوال"
        };

        // هل التعارض على بيانات الشخص نفسه (الجوال) أم على هوية قد تخصّ غيره؟
        public bool IsSamePerson => Field == DuplicateField.Mobile;
    }

    // محرك التسجيل الذاتي: أرقام الطلبات، فحص التكرار، الإنشاء، والانتقالات بين المراحل
    public interface IRegistrationService
    {
        Task<string> GenerateRequestNumberAsync();
        Task<bool> CheckDuplicateByMobileAsync(string mobile, int? excludeRequestId = null);
        Task<bool> CheckDuplicateByStudentIdAsync(string studentId, int? excludeRequestId = null);

        // بيرجّع رقم أول طلب مفتوح يطابق أي من: الرقم الجامعي / رقم الهوية / رقم الجوال،
        // أو null لو مفيش. بيرجّع الرقم مش true/false عشان الرسالة تقول للطالب
        // "عندك طلب رقم كذا" بدل رفض مجهول.
        Task<DuplicateMatch?> FindOpenRequestNumberAsync(string? studentId, string? nationalId, string? mobile, int? excludeRequestId = null);

        // طلب مكتمل لطالب ما زال ساكنًا — يمنع التسجيل مرة أخرى لنفس الشخص
        Task<DuplicateMatch?> FindActiveHousingRequestNumberAsync(string? studentId, string? nationalId, string? mobile);

        // فحص مبكر: يشترط تطابق الرقم الجامعي ورقم الهوية *معًا* على نفس السجل.
        // أضيق من فحص الإرسال عمدًا — ذاك يرفض على أي حقل بمفرده وهو خط الدفاع.
        Task<DuplicateMatch?> FindByStudentAndNationalIdAsync(string studentId, string nationalId);
        Task<Request> CreateRegistrationRequestAsync(int studentId, string requestNumber, string registrationData, int submittedBy);
        Task<bool> ApproveAsSupervisorAsync(int requestId, int supervisorId, string? notes = null);
        Task<bool> RejectAsSupervisorAsync(int requestId, int supervisorId, string? notes = null);
        Task<bool> ApproveAsCyberAsync(int requestId, int cyberId, string? notes = null);
        Task<bool> RejectAsCyberAsync(int requestId, int cyberId, string? notes = null);
        Task<bool> ApproveAsAdminAsync(int requestId, int adminId, string? notes = null);
        Task<bool> RejectAsAdminAsync(int requestId, int adminId, string? notes = null);
        Task<bool> RequestMoreInfoAsync(int requestId, int reviewerId, string notes, string? fromStage = null);
        Task<string?> ResubmitRequestAsync(int requestId, int userId, string? registrationData = null, string? notes = null, string? changesJson = null);
    }
}
