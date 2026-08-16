using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Workflow;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // منطق طوابير الـ workflow — بيستخدم RegistrationService للانتقالات و WorkflowService للسجل
    public class WorkflowActionService : AppServiceBase, IWorkflowActionService
    {
        private readonly IRepository<Request> _requests;
        private readonly IRegistrationService _registration;
        private readonly IWorkflowService _workflow;
        private readonly IAuditService _audit;

        public WorkflowActionService(
            IRepository<Request> requests,
            IRegistrationService registration,
            IWorkflowService workflow,
            IAuditService audit,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _requests = requests;
            _registration = registration;
            _workflow = workflow;
            _audit = audit;
        }

        public async Task<List<QueueItemDto>> GetQueueAsync(string? stage)
        {
            // ⚠️ النظام فيه مجموعتين مصطلحات لنفس المراحل: مسار تسجيل الطالب
            //    (pending_supervisor / pending_cyber / ready_for_provisioning) ومسار
            //    طلبات الموظف (submitted / cyber_review / cyber_approved).
            //    الطابور كان بيدوّر على المجموعة الأولى بس، فطلب عمله موظف مكانش
            //    بيوصل طابور مراجعته أبدًا. بنقبل المصطلحين لنفس المرحلة.
            var myStages = StagesForCurrentUser();
            if (myStages.Length == 0)
                throw UserFriendlyException.Forbidden();

            string[] stages;
            if (!string.IsNullOrEmpty(stage))
            {
                // ⚠️ المرحلة المطلوبة لازم تكون من مراحل المستخدم. قبل كده الـ stage
                //    الجاي من الرابط كان بيتاخد زي ما هو، يعني أي مستخدم يكتب
                //    ?stage=pending_cyber ويشوف طابور مرحلة مش مسؤول عنها.
                if (!myStages.Contains(stage, StringComparer.OrdinalIgnoreCase))
                    throw UserFriendlyException.Forbidden();

                stages = new[] { stage };
            }
            else
            {
                stages = myStages;
            }

            return await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                // النوعين مع بعض — طلب الموظف طلب سكن زي طلب الطالب بالظبط
                .Where(r => (r.RequestType == RequestType.self_registration || r.RequestType == RequestType.housing)
                            && r.Status != null && stages.Contains(r.Status))
                .OrderBy(r => r.SubmittedAt)
                .Select(r => new QueueItemDto
                {
                    Id = r.Id,
                    RequestNumber = r.RequestNumber,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    StudentName = r.Student!.full_name,
                    StudentId = r.Student.student_id,
                    Notes = r.Notes
                })
                .ToListAsync();
        }

        // المراحل اللي بتقف عند المستخدم الحالي — بتتحدّد بصلاحياته مش بدوره،
        // فمستخدم عنده مراجعة الإسكان والأمن السيبراني الاتنين بيشوف الطابورين.
        // المرحلة ليها اسمين حسب المسار (تسجيل الطالب / طلب الموظف) فبنقبل الاتنين.
        // ⚠️ المراحل والصلاحيات صارت في Core/RequestWorkflow.cs — نفس المصدر الذي
        //    تقرأ منه شاشتا الطلبات وفحص الخادم. كانت هنا ثلاث مصفوفات ودالة
        //    switch، فاختلفت عن جدول الانتقالات وسقطت «housing_approved» من
        //    عدّاد الشارة رغم أن لها زر إجراء في الشاشة.
        private string[] StagesForCurrentUser() => RequestWorkflow.StagesFor(
            UnitOfWork.HasPermission("requests.reviewHousing"),
            UnitOfWork.HasPermission("requests.reviewCyber"),
            UnitOfWork.HasPermission("requests.complete"));

        // ⚠️ بعض المراحل يجوز التصرّف فيها بأكثر من صلاحية (مثل cyber_approved:
        //    مراجعة الأمن السيبراني أو إكمال الطلب). كانت الدالة تُرجع واحدة
        //    فقط، فصاحب الصلاحية الثانية يُمنع من مرحلة يراها في شاشته بزر.
        private static string[] PermissionsForStage(string? status) => RequestWorkflow.PermissionsForStage(status);

        // عدد الطلبات المستنية إجراء من المستخدم الحالي — الرقم اللي بيظهر على
        // «كل الطلبات» في القائمة الجانبية. بيقل لوحده مع كل طلب بيتقفل.
        public async Task<int> GetMyQueueCountAsync()
        {
            var stages = StagesForCurrentUser();
            if (stages.Length == 0)
                return 0;

            return await _requests.Query().AsNoTracking()
                .CountAsync(r => (r.RequestType == RequestType.self_registration || r.RequestType == RequestType.housing)
                                 && r.Status != null && stages.Contains(r.Status));
        }

        public async Task<List<StatusCountDto>> GetQueueCountsAsync()
        {
            return await _requests.Query().AsNoTracking()
                .Where(r => r.RequestType == RequestType.self_registration || r.RequestType == RequestType.housing)
                .GroupBy(r => r.Status)
                .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
                .ToListAsync();
        }

        // ⚠️ كان الإجراء بيتحدّد من *دور* المستخدم: admin بيعمل ApproveAsAdmin دايمًا
        //    حتى لو الطلب لسه عند الإسكان، فبيرجع false وتطلع رسالة "لا يمكن اعتماد
        //    الطلب في المرحلة الحالية" من غير سبب واضح. دلوقتي المرحلة هي اللي بتحدّد
        //    الإجراء، والصلاحية هي اللي بتسمح بيه.
        public async Task ApproveAsync(int requestId, string? notes)
        {
            var actorId = RequireActor();
            var stage = await RequireStagePermissionAsync(requestId);

            var result = stage switch
            {
                "requests.reviewHousing" => await _registration.ApproveAsSupervisorAsync(requestId, actorId, notes),
                "requests.reviewCyber" => await _registration.ApproveAsCyberAsync(requestId, actorId, notes),
                _ => await _registration.ApproveAsAdminAsync(requestId, actorId, notes)
            };

            if (!result)
                throw new UserFriendlyException("لا يمكن اعتماد الطلب في المرحلة الحالية", 400);

            await _audit.LogAsync("workflow_approved", "Requests", requestId);
        }

        public async Task RejectAsync(int requestId, string? notes)
        {
            var actorId = RequireActor();
            var stage = await RequireStagePermissionAsync(requestId);

            var result = stage switch
            {
                "requests.reviewHousing" => await _registration.RejectAsSupervisorAsync(requestId, actorId, notes),
                "requests.reviewCyber" => await _registration.RejectAsCyberAsync(requestId, actorId, notes),
                _ => await _registration.RejectAsAdminAsync(requestId, actorId, notes)
            };

            if (!result)
                throw new UserFriendlyException("لا يمكن رفض الطلب في المرحلة الحالية", 400);

            await _audit.LogAsync("workflow_rejected", "Requests", requestId);
        }

        public async Task RequestMoreInfoAsync(int requestId, string? notes, List<string>? fields = null)
        {
            var actorId = RequireActor();
            await RequireStagePermissionAsync(requestId);

            if (string.IsNullOrWhiteSpace(notes))
                throw new UserFriendlyException("الملاحظات مطلوبة لطلب معلومات إضافية", 400);

            // ⚠️ الأسماء بتتفلتر بقائمة الخانات المعروفة قبل ما تتخزّن. من غير ده
            //    أي اسم غلط جاي من الواجهة بيتخزّن ويقفل الطالب على خانة مش موجودة
            //    في الفورم — فيبقى مقفول على كل حاجة ومش فاهم ليه.
            //    فاضية = كل الخانات مفتوحة له (وده حال الطلبات القديمة).
            var cleaned = fields == null ? null : string.Join(",", fields
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => RegistrationDataMapper.NormalizeFieldKey(f.Trim()))
                .Where(RegistrationDataMapper.IsEditableField)
                .Distinct(StringComparer.OrdinalIgnoreCase));

            var result = await _registration.RequestMoreInfoAsync(requestId, actorId, notes, null,
                string.IsNullOrWhiteSpace(cleaned) ? null : cleaned);

            if (!result)
                throw new UserFriendlyException("لا يمكن طلب معلومات إضافية لهذا الطلب", 400);

            await _audit.LogAsync("info_requested", "Requests", requestId);
        }

        public async Task<List<WorkflowHistoryItemDto>> GetHistoryAsync(int requestId)
        {
            var request = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == requestId);
            var studentName = request?.Student?.full_name;

            var history = await _workflow.GetHistoryAsync(requestId);

            // نفس قاعدة RequestService.RedactNotesForRole: مراجع الأمن السيبراني
            // مايقراش ملاحظات رفض حصل في مرحلة تانية قبل ما الطلب يوصله.
            // بنفضّي الملاحظات بس — الخطوة نفسها بتفضل ظاهرة في السجل عشان
            // المسار الزمني يفضل مفهوم.
            // اللي بيراجع مرحلة الأمن السيبراني بس (مش الإسكان كمان) مايقراش ملاحظات
            // رفض حصل في مرحلة قبل ما الطلب يوصله. الأدمن عنده الاتنين فبيشوف الكل.
            var hideOtherStageRejections =
                UnitOfWork.HasPermission("requests.reviewCyber") &&
                !UnitOfWork.HasPermission("requests.reviewHousing") &&
                request?.CyberReviewedAt == null;

            return history.Select(h => new WorkflowHistoryItemDto
            {
                FromStage = h.FromStage,
                ToStage = h.ToStage,
                ActionDate = h.ActionDate,
                // المسارين بيسمّوا الرفض بأسماء مختلفة: تسجيل الطالب = "rejected"،
                // ومسار الموظف = housing_rejected / cyber_rejected. الفحص القديم كان
                // بيشوف "rejected" بس، فلما طلبات الموظف بقى ليها سجل مراحل حقيقي
                // كان رفض الإسكان هيبان لمراجع الأمن السيبراني.
                Notes = (hideOtherStageRejections && IsRejection(h.ToStage) && !IsCyberRejection(h))
                        ? null
                        : h.Notes,
                // لو المنفّذ طالب (دور user) بنعرض اسم الطالب صاحب الطلب — نفس منطق الكود القديم
                ActorName = h.Actor != null
                    ? (h.Actor.UserRoles.Any(ur => ur.Role.Name == "user") && studentName != null ? studentName : h.Actor.full_name ?? h.Actor.UserName)
                    : null
            }).ToList();
        }

        // ----------------------------- Helpers -----------------------------

        private static bool IsRejection(string? stage) =>
            string.Equals(stage, "rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, "housing_rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, "cyber_rejected", StringComparison.OrdinalIgnoreCase);

        // رفض صادر من مرحلة الأمن السيبراني نفسها — ده اللي مراجع السايبر يشوف ملاحظاته
        private static bool IsCyberRejection(Models.WorkflowHistory h) =>
            string.Equals(h.ToStage, "cyber_rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.FromStage, "pending_cyber", StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.FromStage, "cyber_review", StringComparison.OrdinalIgnoreCase);

        private int RequireActor()
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            return actorId;
        }

        // بترجّع الصلاحية المطلوبة لمرحلة الطلب الحالية بعد ما تتأكد إن المستخدم معاه.
        // الفحص هنا مش في الكنترولر لأن نفس الـ endpoint بيخدم المراحل كلها.
        private async Task<string> RequireStagePermissionAsync(int requestId)
        {
            var status = await _requests.Query().AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => r.Status)
                .FirstOrDefaultAsync()
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            var permissions = PermissionsForStage(status);
            if (permissions.Length == 0)
                throw new UserFriendlyException("الطلب مقفول ولا يقبل إجراءات جديدة", 400);

            // أي صلاحية من صلاحيات المرحلة تكفي — والمختارة هي التي يملكها المستخدم
            // فعلًا، لأنها هي التي تحدّد الإجراء المنفَّذ في ApproveAsync/RejectAsync.
            var permission = permissions.FirstOrDefault(p => UnitOfWork.HasPermission(p))
                ?? throw UserFriendlyException.Forbidden();

            return permission;
        }
    }
}
