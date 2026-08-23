using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // ========================================================================
    //  ملف الطالب المجمّع.
    //
    //  ⚠️ الخدمة دي مابتقراش من الدليل ولا من جدول التعهّد بنفسها: بتنادي
    //     IPledgeService و IHousingAccountService. كل مصدر بيفضل ليه مالك
    //     واحد - فلو اتغيّرت طريقة بناء وثيقة التعهّد أو قراءة حساب الشبكة،
    //     بتتغيّر في مكانها والشاشة دي بتاخد التغيير معاها.
    // ========================================================================
    public class StudentFileService : AppServiceBase, IStudentFileService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Request> _requests;
        private readonly IRepository<StudentDeclaration> _declarations;
        private readonly IPledgeService _pledge;
        private readonly IHousingAccountService _housing;
        private readonly IAuditService _audit;
        private readonly ILogger<StudentFileService> _logger;

        public StudentFileService(
            IRepository<Student> students,
            IRepository<Request> requests,
            IRepository<StudentDeclaration> declarations,
            IPledgeService pledge,
            IHousingAccountService housing,
            IAuditService audit,
            ILogger<StudentFileService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _requests = requests;
            _declarations = declarations;
            _pledge = pledge;
            _housing = housing;
            _audit = audit;
            _logger = logger;
        }

        public const string NotRegisteredMessage = "غير مسجَّل في السكن الجامعي.";
        public const string BadQueryMessage =
            "اكتب رقمًا جامعيًا (٩ أرقام تبدأ بـ ٤) أو رقم هوية (١٠ أرقام).";

        public async Task<StudentFileDto> GetAsync(string query, int? requestId)
        {
            var q = (query ?? string.Empty).Trim();

            // ⚠️ نفس قواعد Core/IdentityRules: الشاشة بتفحص بنفس القاعدة قبل
            //    ما تبعت، فالرسالة هنا للي بيبعت من بره الشاشة.
            var byStudentId = IdentityRules.IsValidStudentId(q);
            var byNationalId = !byStudentId && IdentityRules.IsValidNationalId(q);
            if (!byStudentId && !byNationalId)
                throw new UserFriendlyException(BadQueryMessage, 400);

            // ⚠️ Scoped: مشرفة قسم الطالبات مابتشوفش ملف طالب، والعكس. الفحص
            //    ده مش في الشاشة - في الاستعلام نفسه.
            // ⚠️ والمحذوف داخل النتيجة عن قصد (مفيش !IsDeleted): التحقيق بيبدأ
            //    غالبًا بعد ما الطالب يتشال، والشاشة بتعلّم السجل إنه محذوف.
            var student = byStudentId
                ? await Scoped(_students.Query().AsNoTracking()).FirstOrDefaultAsync(s => s.student_id == q)
                : await Scoped(_students.Query().AsNoTracking()).FirstOrDefaultAsync(s => s.national_id == q);

            if (student == null)
                throw UserFriendlyException.NotFound(NotRegisteredMessage);

            var file = new StudentFileDto
            {
                Student = Mapper.Map<StudentDto>(student),
                IsDeleted = student.IsDeleted
            };

            // ---------- الطلبات ----------
            var requests = await _requests.Query().AsNoTracking()
                .Where(r => r.StudentId == student.Id)
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new { r.Id, r.RequestNumber, r.RequestType, r.Status, r.SubmittedAt })
                .ToListAsync();

            // أي طلبات ليها تعهّد - في استعلام واحد لا واحد لكل طلب.
            var ids = requests.Select(r => r.Id).ToList();
            var withPledge = ids.Count == 0
                ? new HashSet<int>()
                : (await _declarations.Query().AsNoTracking()
                    .Where(d => ids.Contains(d.RequestId))
                    .Select(d => d.RequestId).Distinct().ToListAsync()).ToHashSet();

            file.Requests = requests.Select(r => new StudentFileRequestDto
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber,
                RequestType = r.RequestType?.ToString(),
                Status = r.Status,
                SubmittedAt = r.SubmittedAt,
                HasPledge = withPledge.Contains(r.Id)
            }).ToList();

            // ---------- الطلب المعروض ----------
            // ⚠️ الافتراضي: أحدث طلب **له تعهّد**، مش أحدث طلب على الإطلاق.
            //    الموظف فاتح الشاشة عشان التعهّد؛ لو فتحناه على طلب بلا تعهّد
            //    هيفتكر إن الطالب ما وقّعش، وهو موقّع في طلب قبله.
            var selected = requestId.HasValue
                ? file.Requests.FirstOrDefault(r => r.Id == requestId.Value)
                : (file.Requests.FirstOrDefault(r => r.HasPledge) ?? file.Requests.FirstOrDefault());

            if (selected != null)
            {
                file.SelectedRequestId = selected.Id;
                file.SelectedRequestNumber = selected.RequestNumber;
                file.Pledge = await _pledge.GetForRequestAsync(selected.Id);
            }

            // ---------- حساب الشبكة ----------
            // ⚠️ قراءة الدليل ممكن تفشل (شبكة، خدمة موقوفة). الفشل هنا مايوقفش
            //    الملف: الاسم والطلبات والتعهّد موجودين عندنا ومالهمش علاقة
            //    بالدليل. الخانة بترجع فاضية مع علامة، والشاشة بتقول السبب.
            if (!string.IsNullOrWhiteSpace(student.ad_username))
            {
                try
                {
                    var details = await _housing.GetDetailsAsync(student.Id);
                    if (details.AdDetails != null)
                    {
                        file.Ad = new StudentFileAdDto
                        {
                            Username = details.AdDetails.SamAccountName ?? student.ad_username,
                            Enabled = details.AdDetails.AccountEnabled,
                            LastLogonAt = details.AdDetails.LastLogonAt
                        };
                    }
                    else
                    {
                        file.AdUnavailable = true;
                        file.Ad = new StudentFileAdDto { Username = student.ad_username };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Student file: AD lookup failed for {Sam}", student.ad_username);
                    file.AdUnavailable = true;
                    file.Ad = new StudentFileAdDto { Username = student.ad_username };
                }
            }

            // ---------- تسجيل الاطّلاع ----------
            // ⚠️ ده مش سجل «تعديل» - ده سجل **اطّلاع**. أداة بتجمع كل بيانات
            //    طالب في صفحة واحدة لازم يبان مين فتحها وعلى مين وإمتى، وإلا
            //    بقت أداة تتبّع بلا محاسبة. وبيتسجّل قبل ما الرد يخرج.
            await _audit.LogAsync("student_file_viewed", "Students", student.Id);

            return file;
        }
    }
}
