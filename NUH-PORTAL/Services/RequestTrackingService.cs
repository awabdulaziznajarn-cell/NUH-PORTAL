using MapsterMapper;
using Microsoft.EntityFrameworkCore;
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

            return await _requests.Query().AsNoTracking()
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
                    StudentName = r.Student!.full_name
                })
                .ToListAsync();
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
                    Notes = string.Equals(h.ToStage, "rejected", StringComparison.OrdinalIgnoreCase) ? h.Notes : null,
                    ActionDate = h.ActionDate,
                    // حساب الطالب اللي بيتعمل من الـ OTP اسمه بيبقى "طالب" لو ماتلاقاش
                    // في جدول الطلاب وقت التحقق. بنعرض اسم صاحب الطلب الحقيقي بدله
                    // عشان المسارين يبانوا بنفس الشكل.
                    ActorName = IsStudentAccount(h.Actor)
                        ? (request.Student?.full_name ?? h.Actor?.full_name)
                        : (h.Actor?.full_name ?? h.Actor?.UserName)
                }).ToList();
            }
            else
            {
                items = await BuildHistoryFromTimestampsAsync(request);
            }

            return new TrackingDetailsDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                Status = request.Status,
                SubmittedAt = request.SubmittedAt,
                StudentName = request.Student?.full_name,
                AdUsername = request.Student?.ad_username,
                History = items
            };
        }

        // مسار الطلب مبنيًا من تواريخ الطلب — نفس المراحل اللي بتظهر لموظف الإسكان.
        // الملاحظات بتتعرض للطالب في خطوات الرفض بس؛ ملاحظات الموافقة داخلية.
        // حساب الطالب بيتعمل من مسار الـ OTP باسم مستخدم "student_..." — بنستخدمه
        // كعلامة بدل ما نقارن بنص الاسم، لأن الاسم ممكن يتغيّر.
        private static bool IsStudentAccount(User? actor) =>
            actor?.UserName != null && actor.UserName.StartsWith("student_", StringComparison.OrdinalIgnoreCase);

        private async Task<List<TrackingHistoryItemDto>> BuildHistoryFromTimestampsAsync(Models.Request r)
        {
            var list = new List<TrackingHistoryItemDto>();
            var status = r.Status ?? "";

            // كل أسماء المراجعين في استعلام واحد — عشان كل خطوة تبان باسم صاحبها
            // زي مسار تسجيل الطالب بالظبط، مش خطوات بدون اسم.
            var ids = new[] { r.SubmittedBy, r.HousingReviewedBy, r.ReviewedBy, r.CyberReviewedBy,
                              r.ReadyForProvisioningBy, r.CompletedBy }
                      .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

            var names = ids.Count == 0
                ? new Dictionary<int, string>()
                : await _users.Query().AsNoTracking()
                    .Where(u => ids.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.full_name ?? u.UserName ?? "");

            string? NameOf(int? id) => id.HasValue && names.TryGetValue(id.Value, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;

            list.Add(new TrackingHistoryItemDto
            {
                ToStage = "submitted",
                ActionDate = r.SubmittedAt,
                // صاحب الطلب هو الطالب، حتى لو الموظف هو اللي سجّله نيابةً عنه
                ActorName = r.Student?.full_name ?? NameOf(r.SubmittedBy)
            });

            var housingAt = r.HousingReviewedAt ?? r.ReviewedAt;
            if (housingAt.HasValue)
            {
                var rejected = string.Equals(status, "housing_rejected", StringComparison.OrdinalIgnoreCase);
                list.Add(new TrackingHistoryItemDto
                {
                    ToStage = rejected ? "housing_rejected" : "cyber_review",
                    ActionDate = housingAt.Value,
                    Notes = rejected ? r.HousingNotes ?? r.Notes : null,
                    ActorName = NameOf(r.HousingReviewedBy) ?? NameOf(r.ReviewedBy)
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
                    Notes = rejected ? r.CyberNotes : null,
                    ActorName = NameOf(r.CyberReviewedBy)
                });
            }

            if (r.ReadyForProvisioningAt.HasValue)
                list.Add(new TrackingHistoryItemDto
                {
                    ToStage = "ready_for_provisioning",
                    ActionDate = r.ReadyForProvisioningAt.Value,
                    ActorName = NameOf(r.ReadyForProvisioningBy)
                });

            if (r.CompletedAt.HasValue)
                list.Add(new TrackingHistoryItemDto
                {
                    ToStage = "completed",
                    ActionDate = r.CompletedAt.Value,
                    ActorName = NameOf(r.CompletedBy)
                });

            return list.OrderBy(x => x.ActionDate).ToList();
        }


        // ----------------------------- Helpers -----------------------------

        // 05XXXXXXXX أو 5XXXXXXXX → 9665XXXXXXXX (نفس منطق الكنترولر القديم)
        private static string NormalizePhone(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return mobile;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 10 && digits.StartsWith("05"))
                return "9665" + digits[2..];
            if (digits.Length == 9 && digits.StartsWith("5"))
                return "966" + digits;
            return digits;
        }
    }
}
