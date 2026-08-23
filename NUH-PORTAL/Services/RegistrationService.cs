using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly AppDbContext _context;
        private readonly IWorkflowService _workflowService;
        private readonly ADProvisioningService _adProvisioning;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(AppDbContext context, IWorkflowService workflowService, ADProvisioningService adProvisioning, ILogger<RegistrationService> logger)
        {
            _context = context;
            _workflowService = workflowService;
            _adProvisioning = adProvisioning;
            _logger = logger;
        }

        public async Task<string> GenerateRequestNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"{year}-";

            var lastRequest = await _context.Requests
                .Where(r => r.RequestNumber != null && r.RequestNumber.StartsWith(prefix))
                .OrderByDescending(r => r.RequestNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastRequest?.RequestNumber != null)
            {
                var parts = lastRequest.RequestNumber.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out var lastSeq))
                    nextSeq = lastSeq + 1;
            }

            return $"{prefix}{nextSeq:D6}";
        }

        // ⚠️ حُذفت النسخة المحلية: كانت لا تعرف بادئة 00966 فتُخزّن رقمًا بصيغة
        //    ويُبحث عنه بصيغة أخرى. القاعدة الوحيدة في Core/IdentityRules.cs.
        private static string NormalizePhone(string mobile) => IdentityRules.NormalizeMobileOrDigits(mobile);

        // ⚠️ كانت مكتوبة بالإيد هنا: سبع حالات ناقصة منها housing_approved.
        //    والنتيجة مش تجميلية — الطالب اللي طلبه واقف على housing_approved
        //    مكانش بيتحسب «عنده طلب مفتوح»، فيقدر يقدّم طلب تاني ويبقى له
        //    طلبين في النظام.
        //
        //    والمصدر الوحيد Core/RequestWorkflow.OpenStatuses — وهو نفسه
        //    مشتقّ من جدول الانتقالات لا مكتوب بالإيد، فأي مرحلة جديدة تدخل
        //    هنا وحدها. الحالات المنتهية (مكتمل/مرفوض) مش فيها عن قصد:
        //    الطالب المرفوض لازم يقدر يقدّم من جديد.
        private static string[] OpenStatuses => RequestWorkflow.OpenStatuses;

        private static bool PhonesMatch(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return NormalizePhone(a) == NormalizePhone(b);
        }

        // ====================================================================
        //  فحص موحّد للتكرار — بيرجّع رقم الطلب المفتوح المتعارض بدل مجرد true.
        //  ثلاث مفاتيح هوية: الرقم الجامعي، رقم الهوية، رقم الجوال. أي واحد فيهم
        //  يكفي للمنع، لأن الثلاثة بيميّزوا نفس الشخص.
        //  بيغطّي المسارين (طلب الطالب وطلب الموظف) عشان مايبقاش فيه باب خلفي:
        //  الطالب يتقدّم، والموظف يتقدّم له تاني، فيبقى ليه طلبين مفتوحين.
        // ====================================================================
        // الترتيب مقصود: الرقم الجامعي ثم الهوية ثم الجوال — من الأقوى دلالة على
        // الهوية إلى الأضعف، عشان الرسالة تشاور على أهم حقل متعارض.
        private static DuplicateField? MatchField(Models.Student s, string? studentId, string? nationalId, string? mobile)
        {
            if (!string.IsNullOrWhiteSpace(studentId)  && s.student_id  == studentId.Trim())  return DuplicateField.StudentId;
            if (!string.IsNullOrWhiteSpace(nationalId) && s.national_id == nationalId.Trim()) return DuplicateField.NationalId;
            if (!string.IsNullOrWhiteSpace(mobile)     && PhonesMatch(s.phone ?? "", mobile)) return DuplicateField.Mobile;
            return null;
        }

        private static string NumberOf(Request r) => r.RequestNumber ?? $"{r.SubmittedAt.Year}-{r.Id:D6}";

        public async Task<DuplicateMatch?> FindOpenRequestNumberAsync(string? studentId, string? nationalId, string? mobile, int? excludeRequestId = null)
        {
            var open = await _context.Requests
                .Include(r => r.Student)
                .Where(r => OpenStatuses.Contains(r.Status))
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync();

            foreach (var req in open)
            {
                if (excludeRequestId.HasValue && req.Id == excludeRequestId.Value) continue;
                var s = req.Student;
                if (s == null) continue;

                // (كان هنا فحص احتياطي يقرأ الجوال من registration_data. اتشال:
                //  سجل الطالب بيتعمل دايمًا مع الطلب، والسطر اللي فوق بيتخطّى أي
                //  طلب بلا سجل طالب أصلًا — فالفحص ده كان بيضيف مصدر تاني للبيانات
                //  من غير أي فايدة.)
                var field = MatchField(s, studentId, nationalId, mobile);
                if (field != null)
                    return new DuplicateMatch { Field = field.Value, RequestNumber = NumberOf(req) };
            }

            return null;
        }

        // ====================================================================
        //  الطلب المكتمل لا يُعدّ «مفتوحًا»، لكنه لا يعني أن الطالب يستطيع التسجيل
        //  من جديد: هو بالفعل مسجَّل في الإسكان وله حساب شبكة. التسجيل مرة أخرى
        //  كان يُنشئ طلبًا ثانيًا لنفس الشخص وينتهي بمحاولة إنشاء حساب موجود.
        //
        //  الاستثناء المقصود: الطالب الذي غادر السكن أو تخرّج أو حُوِّل — حالته لم
        //  تعد active، فيُسمح له بالتسجيل من جديد دون أي إجراء إضافي. أي أن إعادة
        //  فتح التسجيل لطالب سابق تتم من شاشة «تحديث حالة الطالب»، لا بالتحايل هنا.
        // ====================================================================
        private static readonly string[] HousedStatuses = { "completed", "approved" };

        public async Task<DuplicateMatch?> FindActiveHousingRequestNumberAsync(string? studentId, string? nationalId, string? mobile)
        {
            var done = await _context.Requests
                .Include(r => r.Student)
                .Where(r => HousedStatuses.Contains(r.Status))
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            foreach (var req in done)
            {
                var s = req.Student;
                if (s == null || s.IsDeleted) continue;
                if (s.status != StudentState.active) continue;   // غادر / تخرّج / حُوِّل → يُسمح بالتسجيل

                var field = MatchField(s, studentId, nationalId, mobile);
                if (field != null)
                    return new DuplicateMatch { Field = field.Value, RequestNumber = NumberOf(req) };
            }

            return null;
        }

        public async Task<DuplicateMatch?> FindByStudentAndNationalIdAsync(string studentId, string nationalId)
        {
            if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(nationalId))
                return null;

            var sid = studentId.Trim();
            var nid = nationalId.Trim();

            // الطلبات المفتوحة والمكتملة معًا — أي منهما يمنع طلبًا جديدًا
            var blocking = OpenStatuses.Concat(HousedStatuses).ToArray();

            var req = await _context.Requests
                .Include(r => r.Student)
                .Where(r => blocking.Contains(r.Status)
                            && r.Student != null
                            && !r.Student.IsDeleted
                            && r.Student.student_id == sid
                            && r.Student.national_id == nid)
                .OrderByDescending(r => r.SubmittedAt)
                .FirstOrDefaultAsync();

            if (req == null) return null;

            // الطالب المكتمل الذي غادر أو تخرّج لا يمنع التسجيل من جديد
            if (HousedStatuses.Contains(req.Status) && req.Student!.status != StudentState.active)
                return null;

            return new DuplicateMatch { Field = DuplicateField.StudentId, RequestNumber = NumberOf(req) };
        }

        public async Task<Request> CreateRegistrationRequestAsync(int studentId, string requestNumber, string registrationData, int submittedBy)
        {
            // ⚠️ جنس الطالب يُنسخ على صف الطلب. كان لا يُملأ في هذا المسار إطلاقًا -
            //    يُملأ في مسار طلب الموظف وحده - فيبقى NULL. وتصفية تقسيم الطلاب
            //    والطالبات تقارن student_gender بقسم الموظف، و NULL لا يساوي
            //    'male' ولا 'female' في SQL: فطلب الطالب لا يظهر للمشرف ولا
            //    للمشرفة، ويظهر لمن لا قسم له وحده. التقسيم كان معطَّلًا في هذا
            //    المسار من أوله.
            var studentGender = await _context.Students.AsNoTracking()
                .Where(x => x.Id == studentId)
                .Select(x => x.gender)
                .FirstOrDefaultAsync();

            var request = new Request
            {
                RequestType = RequestType.self_registration,
                StudentId = studentId,
                StudentGender = studentGender,
                SubmittedBy = submittedBy,
                Status = "pending_supervisor",
                RequestNumber = requestNumber,
                RegistrationData = registrationData,
                SubmittedAt = DateTime.UtcNow
            };

            _context.Requests.Add(request);

            // ⚠️ الرقم اللي جه في requestNumber اتولّد قبل الإدخال بلحظات، وممكن
            //    يكون حد تاني أخده في نفس اللحظة. القاعدة في Core/RequestNumberRetry.cs
            //    — بتعيد التوليد وتحفظ تاني بدل ما الطالب ياخد خطأ ٥٠٠.
            // المحاولة الأولى بالرقم اللي جه من المستدعي، وأي محاولة بعدها
            // بتولّد من جديد — وإلا كنا بنعيد نفس الرقم المتصادم للأبد.
            var useCallerNumber = true;
            await RequestNumberRetry.RunAsync(
                generate: () =>
                {
                    if (!useCallerNumber) return GenerateRequestNumberAsync();
                    useCallerNumber = false;
                    return Task.FromResult(requestNumber);
                },
                assign: n => request.RequestNumber = n,
                save: () => _context.SaveChangesAsync());

            await _workflowService.LogTransitionAsync(request.Id, null, "pending_supervisor", submittedBy, "تقديم طلب التسجيل");

            _context.Notifications.Add(new Notification
            {
                request_id = request.Id,
                channel = "in_app",
                recipient_role = "supervisor",
                message = $"تم تقديم طلب تسجيل جديد ({requestNumber})",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return request;
        }

        public async Task<bool> ApproveAsSupervisorAsync(int requestId, int supervisorId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_supervisor")
                return false;

            request.Status = "pending_cyber";
            // ⚠️ مراجعة الإسكان تُسجَّل في HousingReviewedBy/At — هذا هو العمود
            //    الذي يقرأه بقية النظام. وهذا المسار (تسجيل الطالب بنفسه) كان
            //    يكتب في ReviewedBy/At وحدهما، وهما عمودان عامّان قديمان سبقا
            //    أعمدة المراحل.
            //
            //    والنتيجة لم تكن تجميلية: ScopeToRole يُبقي الطلب ظاهرًا لمراجع
            //    الإسكان بعد اعتماده بشرط HousingReviewedAt != null - فكان طلب
            //    التسجيل الذاتي *يختفي من شاشة المشرفة في اللحظة التي تعتمده
            //    فيها*، ولا تراه إلا حين يكتمل. أما طلب الموظف فيظل ظاهرًا،
            //    لأن مساره يكتب في العمود الصحيح.
            //
            //    ReviewedBy/At يبقيان مكتوبَين: صفوفٌ قديمة وشاشة تتبّع الطالب
            //    تقرأ منهما (RequestTrackingService: HousingReviewedAt ?? ReviewedAt).
            request.HousingReviewedBy = supervisorId;
            request.HousingReviewedAt = DateTime.UtcNow;
            request.HousingNotes = notes;
            request.ReviewedBy = supervisorId;
            request.ReviewedAt = DateTime.UtcNow;
            request.Notes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_supervisor", "pending_cyber", supervisorId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "cyber",
                message = $"تمت الموافقة على طلب التسجيل ({reqNum}) من قبل إدارة الإسكان",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectAsSupervisorAsync(int requestId, int supervisorId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_supervisor")
                return false;

            request.Status = "rejected";
            // نفس السبب في الاعتماد فوق: بدون HousingReviewedAt يختفي الطلب
            // الذي رفضته المشرفة من شاشتها فور الرفض.
            request.HousingReviewedBy = supervisorId;
            request.HousingReviewedAt = DateTime.UtcNow;
            request.HousingNotes = notes;
            request.ReviewedBy = supervisorId;
            request.ReviewedAt = DateTime.UtcNow;
            request.Notes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_supervisor", "rejected", supervisorId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم رفض طلب التسجيل ({reqNum}) من قبل إدارة الإسكان",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ApproveAsCyberAsync(int requestId, int cyberId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_cyber")
                return false;

            request.Status = "ready_for_provisioning";
            request.CyberReviewedBy = cyberId;
            request.CyberReviewedAt = DateTime.UtcNow;
            request.CyberNotes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_cyber", "ready_for_provisioning", cyberId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تمت الموافقة على طلب التسجيل ({reqNum}) من قبل إدارة الأمن السيبراني",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectAsCyberAsync(int requestId, int cyberId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "pending_cyber")
                return false;

            request.Status = "rejected";
            request.CyberReviewedBy = cyberId;
            request.CyberReviewedAt = DateTime.UtcNow;
            request.CyberNotes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "pending_cyber", "rejected", cyberId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم رفض طلب التسجيل ({reqNum}) من قبل إدارة الأمن السيبراني",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ApproveAsAdminAsync(int requestId, int adminId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "ready_for_provisioning")
                return false;

            request.HousingReviewedBy = adminId;
            request.HousingReviewedAt = DateTime.UtcNow;
            request.HousingNotes = notes;
            request.ReadyForProvisioningBy = adminId;
            request.ReadyForProvisioningAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var student = await _context.Students.FindAsync(request.StudentId);
            if (student != null)
            {
                var provResult = await _adProvisioning.ProvisionAsync(student, adminId);
                if (!provResult.Success)
                {
                    _logger.LogError("AD provisioning FAILED for self-registration student {Id}: {Error}", student.student_id, provResult.Error);
                    request.Status = "ready_for_provisioning";
                    await _context.SaveChangesAsync();

                    // ⚠️ كان بيرجّع false، والنتيجة إن المستخدم بياخد رسالة
                    //    "لا يمكن اعتماد الطلب في المرحلة الحالية" — وde كذب:
                    //    المرحلة صح تمامًا، اللي فشل هو إنشاء الحساب في الأكتف
                    //    دايركتوري. السبب الحقيقي كان بيتدفن في اللوج بس.
                    //    نفس صيغة الرسالة المستخدمة في RequestService.ReviewAsync.
                    throw new UserFriendlyException(
                        $"فشل إنشاء حساب الشبكة - لم يتم إكمال الطلب: {provResult.Error}", 500);
                }

                _logger.LogInformation("AD account created for self-registration student {Id}: {Sam}", student.student_id, provResult.SamAccountName);
                var syncResult = await _adProvisioning.SyncExtensionAttributesAsync(student, adminId);
                if (!syncResult.Success)
                    _logger.LogWarning("Extension attribute sync failed for student {Id}: {Error}", student.student_id, syncResult.Error);

                if (student.status != StudentState.left)
                    student.status = StudentState.active;
            }

            request.Status = "completed";
            request.CompletedBy = adminId;
            request.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "ready_for_provisioning", "completed", adminId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم إكمال طلب التسجيل ({reqNum}) وتم إنشاء حساب الشبكة",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectAsAdminAsync(int requestId, int adminId, string? notes = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null || request.Status != "ready_for_provisioning")
                return false;

            request.Status = "rejected";
            request.HousingReviewedBy = adminId;
            request.HousingReviewedAt = DateTime.UtcNow;
            request.HousingNotes = notes;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, "ready_for_provisioning", "rejected", adminId, notes);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            _context.Notifications.Add(new Notification
            {
                request_id = requestId,
                channel = "in_app",
                recipient_role = "admin",
                message = $"تم رفض طلب التسجيل ({reqNum}) من قبل الإدارة",
                status = NotificationStatus.pending,
                sent_at = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RequestMoreInfoAsync(int requestId, int reviewerId, string notes, string? fromStage = null, string? infoFields = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null)
                return false;

            var currentStage = fromStage ?? request.Status;
            request.Status = "need_more_info";
            request.Notes = notes;
            request.InfoFields = infoFields;

            await _context.SaveChangesAsync();
            await _workflowService.LogTransitionAsync(requestId, currentStage, "need_more_info", reviewerId, notes);

            return true;
        }

        public async Task<string?> ResubmitRequestAsync(int requestId, int userId, string? registrationData = null, string? notes = null, string? changesJson = null)
        {
            var req = await _context.Requests.FindAsync(requestId);
            if (req == null || req.RequestType != RequestType.self_registration || req.Status != "need_more_info")
                return null;

            var previousStage = await _workflowService.GetPreviousStageAsync(requestId);
            var targetStage = previousStage ?? "pending_supervisor";

            if (!string.IsNullOrEmpty(registrationData))
                req.RegistrationData = registrationData;

            req.Status = targetStage;
            req.ReviewedAt = null;
            req.ReviewedBy = null;
            // ⚠️ الخانات المطلوبة تخصّ جولة المراجعة اللي خلصت. لو سابناها،
            //    الجولة الجاية هتفتح للطالب نفس الخانات القديمة بغضّ النظر عن
            //    اللي المراجع طلبه فعلًا هذه المرة.
            req.InfoFields = null;

            await _context.SaveChangesAsync();
            // الملاحظة بتيجي من طبقة التدفق ومعاها ملخّص التعديلات — من غيرها المراجع
            // كان بيشوف إن الطلب رجعله من غير ما يعرف الطالب غيّر إيه.
            await _workflowService.LogTransitionAsync(requestId, "need_more_info", targetStage, userId,
                string.IsNullOrWhiteSpace(notes) ? "إعادة تقديم بعد طلب معلومات" : notes, changesJson);

            return targetStage;
        }
    }
}
