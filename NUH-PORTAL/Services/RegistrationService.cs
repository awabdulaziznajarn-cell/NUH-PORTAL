using Microsoft.EntityFrameworkCore;
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

        // ⚠️ الحالات "المفتوحة" لازم تشمل مصطلحات المسارين: مسار تسجيل الطالب
        //    (pending_*) ومسار طلبات الموظف (submitted / cyber_review / cyber_approved).
        //    كانت الأولى بس، فطلب مفتوح عمله موظف مكانش بيمنع تكرار.
        //    الحالات المنتهية (مكتمل/مرفوض) مش مفتوحة عن قصد — الطالب المرفوض
        //    لازم يقدر يقدّم من جديد.
        private static readonly string[] OpenStatuses =
        {
            "submitted", "pending_supervisor", "pending_cyber",
            "cyber_review", "cyber_approved", "ready_for_provisioning", "need_more_info"
        };

        private static bool PhonesMatch(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return NormalizePhone(a) == NormalizePhone(b);
        }

        public async Task<bool> CheckDuplicateByMobileAsync(string mobile, int? excludeRequestId = null)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return false;

            var requests = await _context.Requests
                .Include(r => r.Student)
                .Where(r => OpenStatuses.Contains(r.Status))
                .ToListAsync();

            foreach (var req in requests)
            {
                if (excludeRequestId.HasValue && req.Id == excludeRequestId.Value)
                    continue;

                // ⚠️ كان الفحص يقرأ الجوال من registration_data أولًا ثم من سجل
                //    الطالب. الاتنين كانوا بيفترقوا بعد إعادة التقديم، فالفحص كان
                //    ممكن يمسك رقمًا قديمًا. المصدر الوحيد الآن هو سجل الطالب —
                //    وإعادة التقديم تحدّثه عبر RegistrationDataMapper.
                if (req.Student != null && !string.IsNullOrEmpty(req.Student.phone) && PhonesMatch(req.Student.phone, mobile))
                    return true;
            }

            return false;
        }

        public async Task<bool> CheckDuplicateByStudentIdAsync(string studentId, int? excludeRequestId = null)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                return false;

            var student = await _context.Students.FirstOrDefaultAsync(s => s.student_id == studentId);
            if (student == null)
                return false;

            var query = _context.Requests
                .Where(r => r.StudentId == student.Id && OpenStatuses.Contains(r.Status));

            if (excludeRequestId.HasValue)
                query = query.Where(r => r.Id != excludeRequestId.Value);

            return await query.AnyAsync();
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
            var request = new Request
            {
                RequestType = RequestType.self_registration,
                StudentId = studentId,
                SubmittedBy = submittedBy,
                Status = "pending_supervisor",
                RequestNumber = requestNumber,
                RegistrationData = registrationData,
                SubmittedAt = DateTime.UtcNow
            };

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

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

        public async Task<bool> RequestMoreInfoAsync(int requestId, int reviewerId, string notes, string? fromStage = null)
        {
            var request = await _context.Requests.FindAsync(requestId);
            if (request == null)
                return false;

            var currentStage = fromStage ?? request.Status;
            request.Status = "need_more_info";
            request.Notes = notes;

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

            await _context.SaveChangesAsync();
            // الملاحظة بتيجي من طبقة التدفق ومعاها ملخّص التعديلات — من غيرها المراجع
            // كان بيشوف إن الطلب رجعله من غير ما يعرف الطالب غيّر إيه.
            await _workflowService.LogTransitionAsync(requestId, "need_more_info", targetStage, userId,
                string.IsNullOrWhiteSpace(notes) ? "إعادة تقديم بعد طلب معلومات" : notes, changesJson);

            return targetStage;
        }
    }
}
