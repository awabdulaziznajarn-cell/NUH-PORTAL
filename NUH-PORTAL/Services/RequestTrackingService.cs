using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Tracking;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class RequestTrackingService : AppServiceBase, IRequestTrackingService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Request> _requests;
        private readonly IRepository<User> _users;
        private readonly IWorkflowService _workflow;

        public RequestTrackingService(
            IRepository<Student> students,
            IRepository<Request> requests,
            IRepository<User> users,
            IWorkflowService workflow,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _requests = requests;
            _users = users;
            _workflow = workflow;
        }

        // ====================================================================
        //  ⚠️ إخفاء جزئي للاسم في الردود العامة.
        //
        //     شاشة تتبع الطلب متاحة بلا تسجيل دخول، وفيها بحث برقم الجوال بلا
        //     عامل تحقق. الاسم الكامل في الرد كان بيحوّل «رقم جوال مجهول» إلى
        //     «فلان الفلاني، ساكن في السكن الجامعي، وطلبه في المرحلة كذا» —
        //     يعني أداة لربط الأرقام بالهويات لأي حد بيجرّب أرقام.
        //
        //     الاسم الأول يظهر كاملًا، وباقي الأسماء بأول حرف فقط. الاسم الأول
        //     وحده شائع جدًا فلا يميّز شخصًا بعينه، لكنه يكفي صاحب الطلب ليطمئن
        //     أن الطلب طلبه — وكان الإخفاء الكامل يجعل الشاشة غير مفهومة له.
        //     اسم الأب والجد والعائلة هي التي تربط الرقم بالهوية، وتبقى مخفية.
        // ====================================================================
        private static string? MaskName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', parts.Select((p, i) =>
                i == 0 || p.Length <= 1 ? p : p[0] + new string('*', Math.Min(p.Length - 1, 4))));
        }

        public async Task<List<TrackedRequestDto>> TrackByMobileAsync(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                throw new UserFriendlyException("رقم الجوال مطلوب", 400);

            var normalized = NormalizePhone(mobile);

            var studentIds = await _students.Query().AsNoTracking()
                .Where(s => s.phone == mobile || s.phone == normalized)
                .Select(s => s.Id)
                .ToListAsync();

            if (studentIds.Count == 0)
                throw UserFriendlyException.NotFound("لا توجد طلبات مرتبطة بهذا الرقم");

            var list = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                // ⚠️ لازم النوعين: الطالب لما يسجّل بنفسه بيتعمل self_registration،
                //    ولما المشرف يسجّله بيتعمل housing. الاتنين طلب سكن لنفس الطالب
                //    وبيمشوا في نفس دورة الاعتماد — فمن وجهة نظر الطالب مفيش فرق،
                //    وماكانش ينفع طلب المشرف يفضل غير قابل للتتبع.
                .Where(r => (r.RequestType == RequestType.self_registration || r.RequestType == RequestType.housing)
                            && studentIds.Contains(r.StudentId))
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new TrackedRequestDto
                {
                    RequestNumber = r.RequestNumber,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    StudentName = r.Student!.full_name   // بيتخفي بعد الجلب — EF مايترجمش MaskName لـ SQL
                })
                .ToListAsync();

            foreach (var r in list) r.StudentName = MaskName(r.StudentName);
            return list;
        }

        public async Task<TrackingDetailsDto> TrackByNumberAsync(string requestNumber, string? last4)
        {
            if (string.IsNullOrWhiteSpace(requestNumber))
                throw new UserFriendlyException("رقم الطلب مطلوب", 400);

            var digits = new string((last4 ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length != 4)
                throw new UserFriendlyException("آخر ٤ أرقام من رقم الجوال مطلوبة", 400);

            var request = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber
                    && (r.RequestType == RequestType.self_registration || r.RequestType == RequestType.housing));

            // ⚠️ رسالة واحدة للحالتين (رقم طلب غلط / آخر ٤ أرقام غلط) عن قصد.
            //    لو فرّقنا بينهم، الرد نفسه بيبقى أداة تأكيد: المهاجم يعدّي على
            //    الأرقام المتسلسلة وأي رد مختلف بيقوله "الطلب ده موجود" حتى من
            //    غير ما يعرف الجوال. التحقق كمان بيتم على السيرفر مش في الواجهة.
            var phone = new string((request?.Student?.phone ?? "").Where(char.IsDigit).ToArray());
            if (request == null || phone.Length < 4 || !phone.EndsWith(digits, StringComparison.Ordinal))
                throw UserFriendlyException.NotFound("الطلب غير موجود أو البيانات غير متطابقة");

            var history = await _workflow.GetHistoryAsync(request.Id);

            // ⚠️ الطلبات اللي بيعملها موظف مالهاش صفوف في WorkflowHistory — التسجيل
            //    ده موجود في مسار تسجيل الطالب بس. شاشة الموظف بتخفي الفرق لأنها
            //    بتبني المسار من تواريخ الطلب نفسه، فصفحة التتبع كانت الوحيدة اللي
            //    بتبيّن الفراغ: الطلب بيظهر من غير أي مسار.
            //    بنبني نفس المسار هنا من التواريخ لما السجل يبقى فاضي — بيغطّي
            //    الطلبات القديمة كمان، ومش محتاج أي تعديل في البيانات.
            List<TrackingHistoryItemDto> items;
            if (history.Count > 0)
            {
                items = history.Select(h => new TrackingHistoryItemDto
                {
                    ToStage = h.ToStage,
                    Notes = IsStudentVisibleNote(h.ToStage) ? h.Notes : null,
                    ActionDate = h.ActionDate
                }).ToList();
            }
            else
            {
                items = BuildHistoryFromTimestamps(request);
            }

            return new TrackingDetailsDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                Status = request.Status,
                SubmittedAt = request.SubmittedAt,
                StudentName = MaskName(request.Student?.full_name),
                History = items
            };
        }

        // مسار الطلب مبنيًا من تواريخ الطلب — نفس المراحل اللي بتظهر لموظف الإسكان.
        // الملاحظات بتتعرض للطالب في خطوات الرفض بس؛ ملاحظات الموافقة داخلية.
        // المسارين بيسمّوا نفس المرحلة باسمين: تسجيل الطالب = pending_supervisor،
        // ومسار الموظف = submitted. الاتنين معناهم "الطلب اتقدّم".
        private static bool IsSubmission(string? stage) =>
            string.Equals(stage, "submitted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, "pending_supervisor", StringComparison.OrdinalIgnoreCase);

        // نفس الحكاية للرفض: مسار الطالب بيكتب "rejected"، ومسار الموظف بيكتب
        // housing_rejected / cyber_rejected. الملاحظات بتظهر للطالب في دول بس.
        // الملاحظات اللي الطالب يشوفها: سبب الرفض، و«المطلوب استكماله».
        // ملاحظات الموافقة داخلية بين الموظفين.
        // ⚠️ need_more_info كانت ناقصة، فالطالب كان بيقرا «مطلوب استكمال بيانات»
        //    من غير ما يعرف المطلوب إيه بالظبط.
        private static bool IsStudentVisibleNote(string? stage) =>
            IsRejection(stage)
            || string.Equals(stage, "need_more_info", StringComparison.OrdinalIgnoreCase);

        private static bool IsRejection(string? stage) =>
            string.Equals(stage, "rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, "housing_rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, "cyber_rejected", StringComparison.OrdinalIgnoreCase);

        // ⚠️ ماكانتش async فعلًا: الاستعلام الوحيد اللي كان جوّاها اتشال مع
        //    ActorName (المسار عام بلا مصادقة فمينفعش يخرج منه اسم موظف)،
        //    وفضل التوقيع async بلا await — الدالة بتشتغل تزامنيًا ومغلّفة
        //    في Task بلا داعي. بقت متزامنة زي ما هي فعلًا.
        private static List<TrackingHistoryItemDto> BuildHistoryFromTimestamps(Models.Request r)
        {
            var list = new List<TrackingHistoryItemDto>();
            var status = r.Status ?? "";

            // (كان هنا استعلام بيجيب أسماء المراجعين. اتشال مع ActorName —
            //  المسار ده عام بدون مصادقة فمينفعش يخرج منه اسم موظف.)

            list.Add(new TrackingHistoryItemDto
            {
                ToStage = "submitted",
                ActionDate = r.SubmittedAt
            });

            var housingAt = r.HousingReviewedAt ?? r.ReviewedAt;
            if (housingAt.HasValue)
            {
                var rejected = string.Equals(status, "housing_rejected", StringComparison.OrdinalIgnoreCase);
                list.Add(new TrackingHistoryItemDto
                {
                    ToStage = rejected ? "housing_rejected" : "cyber_review",
                    ActionDate = housingAt.Value,
                    Notes = rejected ? r.HousingNotes ?? r.Notes : null
                });
            }

            if (r.CyberReviewedAt.HasValue)
            {
                var rejected = string.Equals(status, "cyber_rejected", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase);
                list.Add(new TrackingHistoryItemDto
                {
                    ToStage = rejected ? "cyber_rejected" : "cyber_approved",
                    ActionDate = r.CyberReviewedAt.Value,
                    Notes = rejected ? r.CyberNotes : null
                });
            }

            if (r.ReadyForProvisioningAt.HasValue)
                list.Add(new TrackingHistoryItemDto
                {
                    ToStage = "ready_for_provisioning",
                    ActionDate = r.ReadyForProvisioningAt.Value
                });

            if (r.CompletedAt.HasValue)
                list.Add(new TrackingHistoryItemDto
                {
                    ToStage = "completed",
                    ActionDate = r.CompletedAt.Value
                });

            return list.OrderBy(x => x.ActionDate).ToList();
        }


        // ----------------------------- Helpers -----------------------------

        // 05XXXXXXXX أو 5XXXXXXXX → 9665XXXXXXXX (نفس منطق الكنترولر القديم)
        // ⚠️ حُذفت النسخة المحلية — القاعدة الوحيدة في Core/IdentityRules.cs.
        private static string NormalizePhone(string mobile) => IdentityRules.NormalizeMobileOrDigits(mobile);
    }
}
