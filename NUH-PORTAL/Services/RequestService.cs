using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Requests;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // كل منطق دورة حياة الطلب: إنشاء → مراجعة إسكان → مراجعة إلكترونية → تجهيز → إكمال (مع AD provisioning)
    public class RequestService : AppServiceBase, IRequestService
    {
        private readonly IRepository<Request> _requests;
        private readonly IRepository<Student> _students;
        private readonly IRepository<User> _users;
        private readonly IRepository<Notification> _notifications;
        private readonly ADProvisioningService _adProvisioning;
        private readonly IAuditService _audit;
        private readonly IRegistrationService _registration;   // لتوليد رقم الطلب بنفس تسلسل مسار الطالب
        private readonly IWorkflowService _workflow;           // سجل المراحل (WorkflowHistory)
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<RequestService> _logger;

        public RequestService(
            IRepository<Request> requests,
            IRepository<Student> students,
            IRepository<User> users,
            IRepository<Notification> notifications,
            ADProvisioningService adProvisioning,
            IAuditService audit,
            IRegistrationService registration,
            IWorkflowService workflow,
            IHttpContextAccessor http,
            ILogger<RequestService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _requests = requests;
            _students = students;
            _users = users;
            _notifications = notifications;
            _adProvisioning = adProvisioning;
            _audit = audit;
            _registration = registration;
            _workflow = workflow;
            _http = http;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  سجل المراحل (WorkflowHistory)
        // ---------------------------------------------------------------------
        //  مسار تسجيل الطالب بيكتب صفًا لكل انتقال (RegistrationService)، لكن مسار
        //  الموظف هنا ماكانش بيكتب أي حاجة — فسجل طلبات الموظف كان فاضي، وصفحة
        //  التتبع بتبنيه من تواريخ الطلب (تقريب مش مصدر حقيقة).
        //  دلوقتي المسارين بيكتبوا في نفس الجدول. الطلبات القديمة لسه سجلها فاضي،
        //  وde شغال لأن RequestTrackingService بيقع على البناء من التواريخ لما
        //  السجل يبقى فاضي — يعني مفيش داتا محتاجة ترحيل.
        //
        //  ⚠️ ActionBy عليه FK لجدول المستخدمين بـ Restrict، فـ 0 بيرمي DbUpdateException.
        //     التسجيل هنا ثانوي بالنسبة للعملية نفسها، فأي فشل بيتسجّل في اللوج
        //     ومابيرميش — مش منطقي إن طلب يتلغي لأن سطر سجل مانفعش يتكتب.
        private async Task LogStageAsync(int requestId, string? fromStage, string toStage, int actorId, string? notes)
        {
            if (actorId <= 0)
            {
                _logger.LogWarning("WorkflowHistory skipped for request {RequestId} ({From} -> {To}): no actor id",
                    requestId, fromStage ?? "(start)", toStage);
                return;
            }

            try
            {
                await _workflow.LogTransitionAsync(requestId, fromStage, toStage, actorId, notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WorkflowHistory write failed for request {RequestId} ({From} -> {To})",
                    requestId, fromStage ?? "(start)", toStage);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  نطاق الطلبات حسب الدور
        // ---------------------------------------------------------------------
        //  قبل كده كل موظف كان بيشوف كل الطلبات في الشاشة، حتى الطلبات اللي لسه
        //  ماوصلتش لمرحلته أصلاً. مراجع الأمن السيبراني مثلاً كان بيشوف طلبات
        //  لسه عند إدارة الإسكان ومش بيقدر يعمل فيها حاجة.
        //
        //  القاعدة دلوقتي:
        //    admin      → كل حاجة (مسؤول النظام)
        //    supervisor → المراحل اللي بتقف عنده + أي طلب عدّى على مراجعة الإسكان
        //    cyber      → المراحل اللي بتقف عنده + أي طلب هو نفسه راجعه
        //
        //  ملحوظة: الطلب مابيختفيش بعد ما الموظف يوافق عليه — بيفضل ظاهر عشان
        //  يقدر يرجع لقراره القديم. اللي مخفي هو اللي ماوصلوش أصلاً.
        //
        //  ⚠️ نفس المرحلة ليها اسمين حسب المسار (تسجيل الطالب / طلب الموظف).
        private static readonly string[] SupervisorStages = { "pending_supervisor", "submitted" };
        private static readonly string[] CyberStages = { "pending_cyber", "cyber_review" };
        private static readonly string[] CompletionStages = { "ready_for_provisioning", "cyber_approved", "housing_approved", "completed" };

        private IQueryable<Request> ScopeToRole(IQueryable<Request> query)
        {
            // ⚠️ تقسيم الطلاب/الطالبات قبل أي حاجة تانية: المشرفة تشوف طلبات
            //    الطالبات بس مهما كانت مرحلتها. الجنس متخزّن على صف الطلب نفسه
            //    (student_gender) لأن الطلب المعلّق ممكن مايكونش له سجل طالب بعد.
            //    والفلتر هنا لأن كل استعلامات الطلبات بتعدّي من الدالة دي.
            var genderScope = UnitOfWork.GetGenderScope();
            if (genderScope != null)
                query = query.Where(r => r.StudentGender == genderScope);

            var actorId = UnitOfWork.GetCurrentUserId();
            var canHousing = UnitOfWork.HasPermission("requests.reviewHousing");
            var canCyber = UnitOfWork.HasPermission("requests.reviewCyber");
            var canComplete = UnitOfWork.HasPermission("requests.complete");

            // مسؤول عن المراحل الثلاثة = مشرف عام على المسار كله، فبيشوف كل الطلبات
            // (ده اللي كان دور admin بيعمله بالظبط).
            if (canHousing && canCyber && canComplete)
                return query;

            // need_more_info مضافة صراحةً: لما المراجع يطلب استكمال بيانات، الطلب
            // بيخرج من مرحلته ومحدش بيسجّل HousingReviewedAt، فكان بيختفي من شاشته
            // تمامًا — يعني بيطلب معلومات وبعدين يفقد الطلب.
            //
            // ⚠️ توكن الطالب (دور user) مابيوصلش هنا أصلاً — الكنترولر بيطلب
            //    requests.view وهي مش من صلاحياته؛ الطالب بيتابع طلبه عبر
            //    /api/Registration و /api/RequestTracking.
            return query.Where(r =>
                (canHousing && ((r.Status != null && SupervisorStages.Contains(r.Status))
                                || r.Status == "need_more_info"
                                || r.HousingReviewedAt != null))
                || (canCyber && ((r.Status != null && CyberStages.Contains(r.Status))
                                || r.CyberReviewedAt != null))
                || (canComplete && r.Status != null && CompletionStages.Contains(r.Status))
                // اللي قدّم الطلب بيفضل شايفه مهما كانت مرحلته
                || r.SubmittedBy == actorId);
        }

        public async Task<List<RequestDto>> GetAllAsync()
        {
            var list = await ScopeToRole(_requests.Query().AsNoTracking()
                    .Include(r => r.Student))
                .ToListAsync();
            return Mapper.Map<List<RequestDto>>(list);
        }


        public async Task<QueryResult<RequestDto>> GetPagedAsync(QueryParams queryParams, string? status, string? requestType, bool mine = false)
        {
            var query = ScopeToRole(_requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .AsQueryable());

            if (!string.IsNullOrEmpty(status))
            {
                // ⚠️ التبويب يفلتر بالمرحلة لا بالنص. المرحلة الواحدة لها اسمان
                //    حسب المسار (تسجيل الطالب / طلب الموظف)، والمقارنة النصّية
                //    كانت تُسقط نصف الطلبات: من يضغط «مراجعة الأمن السيبراني»
                //    لا يرى طلب الطالب الواقف في pending_cyber رغم أنه في نفس
                //    المرحلة تمامًا. الأسماء المكافئة من Core/RequestWorkflow.cs.
                var wanted = RequestWorkflow.Aliases(status);
                query = query.Where(r => r.Status != null && wanted.Contains(r.Status));
            }
            if (!string.IsNullOrEmpty(requestType) && Enum.TryParse<RequestType>(requestType, out var rt))
                query = query.Where(r => r.RequestType == rt);

            // ⚠️ «يحتاج إجراءك»: المراحل التي يملك المستخدم صلاحية التصرّف فيها،
            //    من Core/RequestWorkflow.cs — نفس المصدر الذي يبني به الخادم شارة
            //    القائمة الجانبية وتبني به الشاشة أزرار الصفوف. فلا يظهر في التبويب
            //    صفٌّ بلا زر، ولا يُخفى صفٌّ له زر.
            if (mine)
            {
                var stages = RequestWorkflow.StagesFor(
                    UnitOfWork.HasPermission("requests.reviewHousing"),
                    UnitOfWork.HasPermission("requests.reviewCyber"),
                    UnitOfWork.HasPermission("requests.complete"));

                query = stages.Length == 0
                    ? query.Where(r => false)
                    : query.Where(r => r.Status != null && stages.Contains(r.Status));
            }

            var f = queryParams.FilterText?.Trim();
            if (!string.IsNullOrEmpty(f))
            {
                query = query.Where(r =>
                    (r.RequestNumber != null && r.RequestNumber.Contains(f)) ||
                    (r.Student != null && r.Student.full_name != null && r.Student.full_name.Contains(f)) ||
                    (r.Student != null && r.Student.student_id != null && r.Student.student_id.Contains(f)));
            }

            query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortAsc) switch
            {
                ("id", true) => query.OrderBy(r => r.Id),
                ("id", false) => query.OrderByDescending(r => r.Id),
                ("status", true) => query.OrderBy(r => r.Status),
                ("status", false) => query.OrderByDescending(r => r.Status),
                ("requestnumber", true) => query.OrderBy(r => r.RequestNumber),
                ("requestnumber", false) => query.OrderByDescending(r => r.RequestNumber),
                ("submittedat", true) => query.OrderBy(r => r.SubmittedAt),
                _ => query.OrderByDescending(r => r.SubmittedAt)
            };

            var result = await query.ToPagedResultAsync(queryParams);
            return result.Map<Request, RequestDto>(Mapper);
        }

        public async Task<RequestStatsDto> GetStatsAsync()
        {
            // عدّة واحدة على السيرفر (GroupBy) بدل تحميل كل الطلبات وعدّها في المتصفح
            var counts = await ScopeToRole(_requests.Query().AsNoTracking())
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // ⚠️ العدّاد يجمع مسمّيات المرحلة الواحدة معًا. قبل ذلك كان يعدّ
            //    الاسم الحرفي فقط، فيظهر «مراجعة الأمن السيبراني: ٠» بينما طلب
            //    الطالب واقف فعلًا في تلك المرحلة باسم pending_cyber — والمجموع
            //    الكلي يعدّه، فالأرقام لا تجمع إلى الإجمالي ولا تدلّ على شيء.
            //    الأسماء المكافئة من Core/RequestWorkflow.cs لا مكتوبة هنا.
            int Of(string s)
            {
                var names = RequestWorkflow.Aliases(s);
                return counts.Where(c => c.Status != null && names.Contains(c.Status, StringComparer.OrdinalIgnoreCase))
                             .Sum(c => c.Count);
            }

            return new RequestStatsDto
            {
                Total = counts.Sum(c => c.Count),
                Submitted = Of("submitted"),
                HousingApproved = Of("housing_approved"),
                CyberReview = Of("cyber_review"),
                CyberApproved = Of("cyber_approved"),
                ReadyForProvisioning = Of("ready_for_provisioning"),
                Completed = Of("completed"),
                Rejected = Of("rejected")
            };
        }

        public async Task<RequestDetailsDto> GetDetailsAsync(int id)
        {
            var request = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            // تجميع أسماء المستخدمين اللي شاركوا في الطلب في استعلام واحد
            var userIds = new HashSet<int>();
            if (request.SubmittedBy.HasValue) userIds.Add(request.SubmittedBy.Value);
            if (request.HousingReviewedBy.HasValue) userIds.Add(request.HousingReviewedBy.Value);
            if (request.CyberReviewedBy.HasValue) userIds.Add(request.CyberReviewedBy.Value);
            if (request.ReadyForProvisioningBy.HasValue) userIds.Add(request.ReadyForProvisioningBy.Value);
            if (request.CompletedBy.HasValue) userIds.Add(request.CompletedBy.Value);

            var userNames = userIds.Count > 0
                ? await _users.Query().AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.full_name ?? u.UserName ?? "Unknown")
                : new Dictionary<int, string>();

            string? NameOf(int? userId) =>
                userId.HasValue && userNames.TryGetValue(userId.Value, out var n) ? n : null;

            var dto = new RequestDetailsDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                RequestType = request.RequestType,
                StudentId = request.StudentId,
                Status = request.Status,
                Notes = request.Notes,
                SubmittedAt = request.SubmittedAt,
                ReviewedAt = request.ReviewedAt,
                ReviewedBy = request.ReviewedBy,
                HousingReviewedAt = request.HousingReviewedAt,
                HousingReviewedBy = request.HousingReviewedBy,
                HousingNotes = request.HousingNotes,
                CyberReviewedAt = request.CyberReviewedAt,
                CyberReviewedBy = request.CyberReviewedBy,
                CyberNotes = request.CyberNotes,
                ReadyForProvisioningAt = request.ReadyForProvisioningAt,
                ReadyForProvisioningBy = request.ReadyForProvisioningBy,
                CompletedAt = request.CompletedAt,
                CompletedBy = request.CompletedBy,
                BulkRequestId = request.BulkRequestId,
                RequestedByRole = request.RequestedByRole,
                Student = Mapper.Map<NUH_PORTAL.DTOs.Students.StudentDto>(request.Student),
                SubmittedByName = NameOf(request.SubmittedBy),
                HousingReviewedByName = NameOf(request.HousingReviewedBy),
                CyberReviewedByName = NameOf(request.CyberReviewedBy),
                ReadyForProvisioningByName = NameOf(request.ReadyForProvisioningBy),
                CompletedByName = NameOf(request.CompletedBy)
            };

            // آخر إعادة تقديم من الطالب — الشاشة تعلّم الحقول التي غيّرها.
            // المصدر: WorkflowHistory.changes_json (يُكتب في RegistrationFlowService).
            var (changesJson, changedAt) = await _workflow.GetLastChangesAsync(id);
            if (!string.IsNullOrWhiteSpace(changesJson))
            {
                dto.StudentEdits = RegistrationDataMapper.DeserializeChanges(changesJson)
                    .Select(c => new NUH_PORTAL.DTOs.Requests.StudentEditDto
                    {
                        Field = c.Field, Label = c.Label, Old = c.Old, New = c.New
                    }).ToList();
                dto.StudentEditedAt = changedAt;
            }

            RedactNotesForRole(dto, request);
            return dto;
        }

        // ====================================================================
        //  إخفاء سبب الرفض عن الدور اللي الطلب ماوصلوش أصلاً.
        //  الطلب اللي رفضته إدارة الإسكان ما بيعديش للأمن السيبراني، فمفيش
        //  سبب إن مراجع الأمن السيبراني يقرأ ملاحظات مرحلة ما شافهاش.
        //  القاعدة (متفق عليها مع الجهة):
        //    admin       → يشوف كل حاجة
        //    supervisor  → يشوف رفض الإسكان ورفض الأمن السيبراني
        //    cyber       → يشوف رفضه هو بس؛ ومايشوفش رفضًا حصل قبل ما يوصله
        //    الطالب      → بيقرا من /api/RequestTracking (مسار منفصل)
        //  الإخفاء هنا على السيرفر مش في الواجهة — إخفاء بالـ JS مش حماية.
        // ====================================================================
        private void RedactNotesForRole(RequestDetailsDto dto, Request request)
        {
            // اللي بيراجع الأمن السيبراني بس — الأدمن عنده مراجعة الإسكان كمان فبيشوف الكل
            if (!(UnitOfWork.HasPermission("requests.reviewCyber")
                  && !UnitOfWork.HasPermission("requests.reviewHousing")))
                return;

            var rejected = string.Equals(dto.Status, "rejected", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(dto.Status, "housing_rejected", StringComparison.OrdinalIgnoreCase);

            // CyberReviewedAt != null معناها إن الأمن السيبراني كان طرفًا في الطلب فعلاً،
            // ساعتها بيشوف كل حاجة عادي.
            if (rejected && request.CyberReviewedAt == null)
            {
                dto.Notes = null;
                dto.HousingNotes = null;
            }
        }

        public async Task<List<RequestDto>> GetPendingAsync()
        {
            var list = await _requests.Query().AsNoTracking()
                .Where(r => r.Status == "submitted")
                .ToListAsync();
            return Mapper.Map<List<RequestDto>>(list);
        }

        public async Task<RequestDto> CreateAsync(RequestCreateDto dto)
        {
            var role = UnitOfWork.GetCurrentUserRole()?.ToLower();
            if (!UnitOfWork.HasPermission("requests.create"))
                throw UserFriendlyException.Forbidden();

            var actorId = UnitOfWork.GetCurrentUserId();

            var request = Mapper.Map<Request>(dto);

            // ⚠️ نفس فحص التكرار اللي في مسار الطالب — مكانش موجود هنا خالص، فالموظف
            //    كان يقدر يعمل طلب تاني لطالب عنده طلب مفتوح من غير أي تنبيه.
            //    الفحص بيقارن بالرقم الجامعي ورقم الهوية ورقم الجوال، وبيرجّع رقم
            //    الطلب المتعارض عشان الموظف يفتحه بدل ما يعمل نسخة تانية.
            var dupStudent = await _students.Query().AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (dupStudent != null)
            {
                var dup = await _registration.FindOpenRequestNumberAsync(
                    dupStudent.student_id, dupStudent.national_id, dupStudent.phone);
                if (dup != null)
                    throw new UserFriendlyException(
                        $"يوجد طلب قائم بالفعل لهذا الطالب رقمه {dup.RequestNumber} (تطابق في {dup.FieldLabel}) - راجعه قبل إنشاء طلب جديد.", 400);
            }

            // اللي بيراجع مرحلة الإسكان لما ينشئ الطلب بنفسه → موافقة الإسكان تلقائيًا
            // والتحويل مباشرة للمراجعة الإلكترونية (مالوش معنى يراجع طلب كتبه بإيده).
            // جنس الطالب بيتخزّن على الطلب وقت الإنشاء — منه بيتحدد المشرف المسؤول
            request.StudentGender = dupStudent?.gender;

            var isHousingCreator = UnitOfWork.HasPermission("requests.reviewHousing");
            // ⚠️ الطلبات اللي بيعملها الموظف كانت بتتخزّن بـ request_number = NULL،
            //    بينما مسار تسجيل الطالب بيولّد رقم. النتيجة: الشاشة كانت بتعرض رقم
            //    محسوب من رقم الصف وقت العرض، والطالب يكتبه في التتبع فمايتلاقاش —
            //    لأنه ماكانش متخزّن أصلاً. بنولّده هنا بنفس التسلسل المشترك.
            if (string.IsNullOrWhiteSpace(request.RequestNumber))
                request.RequestNumber = await _registration.GenerateRequestNumberAsync();

            request.Status = isHousingCreator ? "cyber_review" : "submitted";
            request.SubmittedBy = actorId > 0 ? actorId : null;
            request.SubmittedAt = DateTime.UtcNow;
            request.RequestedByRole = role;

            if (isHousingCreator)
            {
                request.HousingReviewedBy = actorId;
                request.HousingReviewedAt = DateTime.UtcNow;
                request.ReviewedBy = actorId;
                request.ReviewedAt = DateTime.UtcNow;
            }

            try
            {
                await _requests.AddAsync(request);
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547 && sqlEx.Message.Contains("FOREIGN KEY"))
                    throw new UserFriendlyException("الطالب غير موجود", 409);
                if (sqlEx.Number == 547 && sqlEx.Message.Contains("CHECK"))
                    throw new UserFriendlyException("بيانات الطلب غير صالحة", 400);
                throw;
            }

            // نفس أسماء أحداث الـ audit القديمة بالحرف (متسجلة كده في تقارير الـ AuditLogs)
            await _audit.LogAsync("housing_approve_request", "Requests", request.Id);
            if (isHousingCreator)
                await _audit.LogAsync("submit_cyber_review", "Requests", request.Id);

            // سجل المراحل — بنفس الخطوات اللي كانت بتتبني من التواريخ، عشان الطلبات
            // القديمة والجديدة يبانوا بنفس الشكل بالظبط في صفحة التتبع.
            await LogStageAsync(request.Id, null, "submitted", actorId, "تقديم الطلب");
            if (isHousingCreator)
                await LogStageAsync(request.Id, "submitted", "cyber_review", actorId, "موافقة إدارة الإسكان");

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            var notifRoles = isHousingCreator ? new[] { "cyber" } : new[] { "admin", "supervisor", "cyber" };
            var notifMsg = isHousingCreator
                ? $"تم تقديم طلب جديد وإحالته للمراجعة الإلكترونية ({reqNum})"
                : $"تم تقديم طلب جديد ({reqNum})";
            await NotifyRolesAsync(request.Id, notifRoles, notifMsg);

            return Mapper.Map<RequestDto>(request);
        }

        public async Task<RequestDto> ReviewAsync(int id, ReviewDto dto)
        {
            var req = await _requests.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            var actorId = UnitOfWork.GetCurrentUserId();
            var oldStatus = req.Status;

            var canHousing = UnitOfWork.HasPermission("requests.reviewHousing");
            var canCyber = UnitOfWork.HasPermission("requests.reviewCyber");
            var canComplete = UnitOfWork.HasPermission("requests.complete");

            // ⚠️ جدول الانتقالات كان مكتوبًا هنا وفي شاشتَي الطلبات — ثلاث نسخ
            //    تفارقت فعلًا. صار تعريفه الوحيد في Core/RequestWorkflow.cs،
            //    والواجهة تقرأ منه عبر window.__WF. الفحص هنا هو الحقيقي.
            var allowed = RequestWorkflow.IsAllowed(req.Status, dto.Status, canHousing, canCyber, canComplete);

            if (!allowed)
                throw new UserFriendlyException("هذا الإجراء غير متاح على الطلب في مرحلته الحالية", 400);

            // ====================================================================
            //  ⚠️ كان مسار طلبات الموظف فيه وقفتين وسيطتين مالهمش قرار:
            //       موافقة الإسكان → (ضغطة) → مراجعة السيبراني
            //       موافقة السيبراني → (ضغطة) → جاهز لإنشاء الحساب
            //
            //     الأثر مش بطء بس: الطلب بعد موافقة الإسكان ماكانش بيظهر عند الأمن
            //     السيبراني أصلًا — لا في طابوره ولا في إشعاراته. والوحيد اللي يقدر
            //     يحرّكه هو صاحب صلاحية «إكمال الطلب». يعني المسؤول عن *إنهاء*
            //     الطلبات كان بيتحكم في *وصولها للمراجعة الأمنية*، ولو نسي أو قرر
            //     ما يحوّلش، المراجعة الأمنية تتخطّى بلا أثر ظاهر.
            //
            //     مسار تسجيل الطالب ماكانش فيه المشكلة دي أصلًا: الموافقة بتنقل
            //     الطلب للمكتب اللي بعده في نفس اللحظة. المسارين بقوا متطابقين.
            // ====================================================================
            var effectiveStatus = (req.Status, dto.Status) switch
            {
                ("submitted", "housing_approved") => "cyber_review",
                ("cyber_review", "cyber_approved") => "ready_for_provisioning",
                _ => dto.Status
            };

            req.Status = effectiveStatus;
            req.Notes = dto.Notes;

            Student? student = null;

            if (dto.Status == "housing_approved" || dto.Status == "housing_rejected")
            {
                req.HousingReviewedBy = actorId;
                req.HousingReviewedAt = DateTime.UtcNow;
                req.HousingNotes = dto.Notes;
            }
            else if (dto.Status == "cyber_approved" || dto.Status == "cyber_rejected")
            {
                req.CyberReviewedBy = actorId;
                req.CyberReviewedAt = DateTime.UtcNow;
                req.CyberNotes = dto.Notes;
            }
            else if (dto.Status == "ready_for_provisioning")
            {
                req.ReadyForProvisioningBy = actorId;
                req.ReadyForProvisioningAt = DateTime.UtcNow;
            }

            // المرحلة اللي اتخطّت لازم تتختم كمان، وإلا يظهر الطلب «جاهز للإنشاء»
            // من غير تاريخ للجاهزية وتبان فجوة في سجل المراجعات.
            if (effectiveStatus == "ready_for_provisioning" && dto.Status == "cyber_approved")
            {
                req.ReadyForProvisioningBy = actorId;
                req.ReadyForProvisioningAt = DateTime.UtcNow;
            }
            else if (dto.Status == "completed")
            {
                student = await _students.GetByIdAsync(req.StudentId);
                if (student != null)
                {
                    var ctx = _http.HttpContext;
                    var ip = ctx?.Connection.RemoteIpAddress?.ToString();
                    var ua = ctx?.Request.Headers["User-Agent"].ToString();

                    var provResult = await _adProvisioning.ProvisionAsync(student, actorId, ip, ua);
                    if (!provResult.Success)
                    {
                        _logger.LogError("AD provisioning FAILED for student {Id}: {Error} | StackTrace: {Stack}",
                            student.student_id, provResult.Error, provResult.StackTrace);
                        // الرسالة فيها سبب الفشل — من غير stack trace للعميل (كان بيتسرب قبل كده)
                        throw new UserFriendlyException($"فشل إنشاء حساب الشبكة - لم يتم إكمال الطلب: {provResult.Error}", 500);
                    }

                    _logger.LogInformation("AD account created for student {Id}: {Sam}", student.student_id, provResult.SamAccountName);
                    var syncResult = await _adProvisioning.SyncExtensionAttributesAsync(student, actorId);
                    if (!syncResult.Success)
                        _logger.LogWarning("Extension attribute sync failed for student {Id}: {Error}", student.student_id, syncResult.Error);
                }

                req.CompletedBy = actorId;
                req.CompletedAt = DateTime.UtcNow;
                if (student != null && student.status != StudentState.left)
                    student.status = StudentState.active;
            }

            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedBy = actorId;

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547)
                    throw new UserFriendlyException("خطأ في تحديث الطلب", 409);
                throw;
            }

            var reviewAction = dto.Status switch
            {
                "housing_approved" => "housing_approve_request",
                "housing_rejected" => "housing_reject_request",
                "cyber_review" => "submit_cyber_review",
                "cyber_approved" => "cyber_approve_request",
                "cyber_rejected" => "cyber_reject_request",
                "ready_for_provisioning" => "ready_for_provisioning_request",
                "bulk_registration_completed" => "bulk_registration_completed",
                "completed" => "complete_request",
                _ => null
            };

            // سجل المراحل لكل انتقال — نفس اللي بيعمله مسار تسجيل الطالب.
            // ملاحظات الرفض بتتخزّن هنا عشان تظهر للطالب في التتبع؛ ملاحظات
            // الموافقة داخلية وRequestTrackingService بيخفيها.
            // dto.Status هنا مستحيل يكون فاضي (جدول الانتقالات فوق كان هيرمي)،
            // بس الفحص مكتوب صراحةً عشان الكمبايلر ما يحذّرش من nullable.
            // الحالة الفعلية هي اللي بتتسجّل — لو الموافقة نقلت الطلب مرحلتين
            // (اعتماد + إحالة) يبان في السجل انتقال واحد صحيح مش انتقال ناقص.
            if (!string.IsNullOrEmpty(effectiveStatus) && oldStatus != effectiveStatus)
                await LogStageAsync(req.Id, oldStatus, effectiveStatus, actorId, dto.Notes);

            if (reviewAction != null)
            {
                var changes = new List<AuditChangeLog>();
                if (oldStatus != effectiveStatus)
                    changes.Add(new AuditChangeLog { FieldName = "Status", OldValue = oldStatus, NewValue = effectiveStatus });

                await _audit.LogAsync(reviewAction, "Requests", req.Id, changes.Count > 0 ? changes : null);

                var reqNum = req.RequestNumber ?? $"{DateTime.UtcNow.Year}-{req.Id:D6}";
                var notifRoles = dto.Status switch
                {
                    // الموافقة بقت بتحوّل الطلب للسيبراني فورًا — فالإشعار يروح له
                    "housing_approved" => new[] { "cyber", "admin" },
                    "housing_rejected" => new[] { "admin", req.RequestedByRole ?? "supervisor" },
                    "cyber_review" => new[] { "cyber" },
                    // وموافقة السيبراني بقت بتخلّي الطلب جاهزًا للإنشاء فورًا
                    "cyber_approved" => new[] { "admin", "supervisor", req.RequestedByRole ?? "admin" },
                    "cyber_rejected" => new[] { req.RequestedByRole ?? "admin" },
                    "ready_for_provisioning" => new[] { "admin", "supervisor" },
                    "completed" => new[] { "admin", "supervisor", req.RequestedByRole ?? "admin" },
                    _ => Array.Empty<string>()
                };
                var notifMsg = dto.Status switch
                {
                    "housing_approved" => $"تمت موافقة إدارة الإسكان على الطلب ({reqNum}) وأُحيل إلى الأمن السيبراني",
                    "housing_rejected" => $"تم رفض الطلب ({reqNum}) من قبل لجنة الإسكان",
                    "cyber_review" => $"تم إحالة الطلب ({reqNum}) إلى المراجعة الإلكترونية",
                    "cyber_approved" => $"تمت موافقة الأمن السيبراني على الطلب ({reqNum}) وأصبح جاهزًا لإنشاء حساب الشبكة",
                    "cyber_rejected" => $"تم الرفض الإلكتروني للطلب ({reqNum})",
                    "ready_for_provisioning" => $"الطلب ({reqNum}) جاهز لإنشاء حساب شبكة السكن",
                    "completed" => $"تم إكمال الطلب ({reqNum})",
                    _ => ""
                };
                await NotifyRolesAsync(req.Id, notifRoles, notifMsg);
            }

            return Mapper.Map<RequestDto>(req);
        }

        public async Task<RequestDto> UpdateBulkIdAsync(int id, UpdateRequestDto dto)
        {
            var req = await _requests.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            if (dto.BulkRequestId.HasValue)
                req.BulkRequestId = dto.BulkRequestId.Value;

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547)
                    throw new UserFriendlyException("خطأ في تحديث الطلب", 409);
                throw;
            }

            return Mapper.Map<RequestDto>(req);
        }

        // ----------------------------- Helpers -----------------------------

        private async Task NotifyRolesAsync(int requestId, string[] roles, string message)
        {
            if (roles.Length == 0) return;

            foreach (var r in roles)
            {
                await _notifications.AddAsync(new Notification
                {
                    request_id = requestId,
                    channel = "in_app",
                    recipient_role = r,
                    message = message,
                    status = NotificationStatus.pending,
                    sent_at = DateTime.UtcNow
                });
            }

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                throw new UserFriendlyException("خطأ في الإشعارات", 409);
            }
        }
    }
}
