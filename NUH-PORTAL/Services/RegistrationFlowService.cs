using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Registration;
using NUH_PORTAL.DTOs.Workflow;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // تدفق التسجيل الذاتي — بيستخدم RegistrationService (المحرك) و WorkflowService (السجل)
    public class RegistrationFlowService : AppServiceBase, IRegistrationFlowService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<User> _users;
        private readonly IRepository<Request> _requests;
        private readonly IRepository<StudentDeclaration> _declarations;
        private readonly IRegistrationService _registration;
        private readonly IWorkflowService _workflow;
        private readonly IHttpContextAccessor _http;
        private readonly ILookupResolver _lookups;

        public RegistrationFlowService(
            IRepository<Student> students,
            IRepository<User> users,
            IRepository<Request> requests,
            IRepository<StudentDeclaration> declarations,
            IRegistrationService registration,
            IWorkflowService workflow,
            IHttpContextAccessor http,
            ILookupResolver lookups,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _users = users;
            _requests = requests;
            _declarations = declarations;
            _registration = registration;
            _workflow = workflow;
            _http = http;
            _lookups = lookups;
        }

        private (string? ip, string ua) ClientInfo()
        {
            var ctx = _http.HttpContext;
            return (ctx?.Connection.RemoteIpAddress?.ToString(), ctx?.Request.Headers.UserAgent.ToString() ?? "");
        }

        // الأدوار الوظيفية اللي ليها حق يشوفوا طلب أي طالب. أي دور تاني (بما فيه
        // دور فاضي أو غير معروف) بيتعامل كطالب — منع افتراضي مش سماح افتراضي.
        private static bool IsStaffRole(string? role) =>
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "supervisor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "cyber", StringComparison.OrdinalIgnoreCase);

        // Students.phone متخزّن 9665XXXXXXXX، لكن الطالب بيكتب 05XXXXXXXX في شاشة
        // الـ OTP وده اللي بيتخزّن في Users.mobile. من غير التطبيع ده أي مقارنة بين
        // الاتنين بتفشل، وتحقق الملكية تحت كان هيرفض صاحب الطلب نفسه.
        // نفس منطق normalizeSaudiMobile في register-form.html بالحرف.
        private static string? NormalizeMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return null;
            var d = new string(mobile.Where(char.IsDigit).ToArray());
            if (d.StartsWith("00966")) d = d[2..];
            if (d.StartsWith("966")) d = d[3..];
            if (d.StartsWith("0")) d = d[1..];
            return d.Length == 9 && d[0] == '5' ? "966" + d : null;
        }

        private int RequireActor()
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);
            return actorId;
        }

        public async Task<StartRegistrationResultDto> StartAsync(StartRegistrationRequest request)
        {
            var actorId = RequireActor();

            var student = await _students.FindAsync(s => s.student_id == request.StudentId);
            if (student == null)
            {
                student = new Student
                {
                    student_id = request.StudentId,
                    full_name = ExtractRegField(request.RegistrationData, "full_name"),
                    full_name_english = ExtractRegField(request.RegistrationData, "full_name_english"),
                    national_id = ExtractRegField(request.RegistrationData, "national_id"),
                    phone = ExtractRegField(request.RegistrationData, "phone") ?? ExtractRegField(request.RegistrationData, "mobile"),
                    gender = GenderHelper.Parse(ExtractRegField(request.RegistrationData, "gender")),
                    college = ExtractRegField(request.RegistrationData, "college"),
                    department = ExtractRegField(request.RegistrationData, "department"),
                    academic_level = ExtractRegField(request.RegistrationData, "academic_level"),
                    housing_building = ExtractRegField(request.RegistrationData, "housing_building"),
                    floor_number = ExtractRegField(request.RegistrationData, "floor_number"),
                    room_number = ExtractRegField(request.RegistrationData, "room_number"),
                    apartment_number = ExtractRegField(request.RegistrationData, "apartment_number"),
                    status = (ExtractRegField(request.RegistrationData, "status") ?? "active").Trim().ToLowerInvariant() switch
                    {
                        "left" => StudentState.left,
                        "inactive" => StudentState.inactive,
                        _ => StudentState.active
                    },
                    created_at = DateTime.UtcNow,
                    created_by = actorId
                };
                await _lookups.ApplyAsync(student); // FK ids من الأكواد (dual-write)
                await _students.AddAsync(student);
                await UnitOfWork.SaveAsync();
            }

            if (await _registration.CheckDuplicateByStudentIdAsync(request.StudentId))
                throw new UserFriendlyException("لديك طلب تسجيل قيد المراجعة بالفعل", 400);

            var mobile = ExtractRegField(request.RegistrationData, "mobile")
                         ?? ExtractRegField(request.RegistrationData, "phone");

            if (!string.IsNullOrEmpty(mobile) && await _registration.CheckDuplicateByMobileAsync(mobile))
                throw new UserFriendlyException("رقم الجوال مستخدم بالفعل في طلب تسجيل آخر", 400);

            var requestNumber = await _registration.GenerateRequestNumberAsync();
            var registrationDataJson = System.Text.Json.JsonSerializer.Serialize(request.RegistrationData ?? new { });

            var newRequest = await _registration.CreateRegistrationRequestAsync(
                student.Id, requestNumber, registrationDataJson, actorId);

            var (ip, ua) = ClientInfo();
            await _workflow.LogAuditAsync(actorId, "registration_created", "Requests", newRequest.Id, ip, ua);

            return new StartRegistrationResultDto
            {
                Message = "تم تقديم طلب التسجيل بنجاح",
                RequestId = newRequest.Id,
                RequestNumber = newRequest.RequestNumber
            };
        }

        public async Task AcceptDeclarationsAsync(int requestId, AcceptDeclarationsRequest request)
        {
            var actorId = RequireActor();

            _ = await _requests.GetByIdAsync(requestId)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            var (ip, ua) = ClientInfo();

            var declaration = new StudentDeclaration
            {
                RequestId = requestId,
                DeclarationAccepted = request.DeclarationAccepted,
                PolicyAccepted = request.PolicyAccepted,
                PolicyVersion = request.PolicyVersion ?? "1.0",
                AcceptedDate = DateTime.UtcNow,
                IPAddress = ip,
                UserAgent = ua
            };

            await _declarations.AddAsync(declaration);
            await UnitOfWork.SaveAsync();

            await _workflow.LogAuditAsync(actorId, "declaration_accepted", "StudentDeclarations", declaration.Id, ip, ua);
        }

        public async Task<List<MyRequestListItemDto>> GetMyRequestsAsync(string? mobile)
        {
            var actorId = RequireActor();
            var userRole = UnitOfWork.GetCurrentUserRole();

            IQueryable<Request> query = _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .Where(r => r.RequestType == RequestType.self_registration);

            // منع افتراضي: بنسمح بالعرض الكامل للأدوار الوظيفية المعروفة بس.
            // الشرط القديم كان "لو الدور طالب فلتر" — يعني توكن بدور فاضي أو دور
            // مش معروف كان بياخد كل الطلبات. دلوقتي أي دور مش في القائمة دي
            // بيتعامل كطالب ومابيشوفش غير طلباته.
            if (!IsStaffRole(userRole))
            {
                // ⚠️ رقم الجوال بيتاخد من التوكن، مش من الـ query string.
                //    قبل كده أي طالب كان يقدر يبعت ?mobile=رقم-حد-تاني ويقرا طلباته.
                //    البارامتر mobile اللي جاي من العميل بيتجاهل للطالب تمامًا.
                var myMobile = await _users.Query().AsNoTracking()
                    .Where(u => u.Id == actorId)
                    .Select(u => u.mobile)
                    .FirstOrDefaultAsync();

                // بنقارن بالشكلين (اللي اتكتب واللي بعد التطبيع) — قائمة ثابتة عشان
                // الترجمة لـ SQL تبقى مضمونة بدل شرط فيه فحص null على متغيّر ملتقط.
                var myPhones = new List<string>();
                if (!string.IsNullOrWhiteSpace(myMobile)) myPhones.Add(myMobile);
                var myMobileNorm = NormalizeMobile(myMobile);
                if (myMobileNorm != null && !myPhones.Contains(myMobileNorm)) myPhones.Add(myMobileNorm);

                var myStudentIds = myPhones.Count == 0
                    ? new List<int>()
                    : await _students.Query().AsNoTracking()
                        .Where(s => s.phone != null && myPhones.Contains(s.phone))
                        .Select(s => s.Id)
                        .ToListAsync();

                // الطلب ممكن يكون قدّمه الطالب بنفسه (SubmittedBy) أو موظف نيابة عنه،
                // فبنجمع الحالتين بدل ما نعتمد على واحدة.
                query = query.Where(r => r.SubmittedBy == actorId || myStudentIds.Contains(r.StudentId));
            }

            return await query
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new MyRequestListItemDto
                {
                    Id = r.Id,
                    RequestNumber = r.RequestNumber,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    ReviewedAt = r.ReviewedAt,
                    StudentName = r.Student!.full_name,
                    StudentId = r.Student.student_id,
                    StudentPhone = r.Student.phone
                })
                .ToListAsync();
        }

        public async Task<MyRequestDetailDto> GetMyRequestDetailAsync(int requestId)
        {
            var request = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.RequestType == RequestType.self_registration)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            // ⚠️ الميثود دي كانت بتدّي أي رقم طلب لأي طالب مسجّل دخول — يعني تغيير
            //    الـ id في الرابط كان بيرجّع بيانات طالب تاني كاملة (رقم الهوية والجوال).
            //    الموظفين بيوصلوا عادي؛ الطالب لازم يكون صاحب الطلب.
            //    بنرجّع "غير موجود" مش "ممنوع" عشان مانأكّدش وجود الطلب أصلاً.
            var actorId = RequireActor();
            if (!IsStaffRole(UnitOfWork.GetCurrentUserRole()))
            {
                var owns = request.SubmittedBy == actorId;
                if (!owns)
                {
                    var myMobile = await _users.Query().AsNoTracking()
                        .Where(u => u.Id == actorId)
                        .Select(u => u.mobile)
                        .FirstOrDefaultAsync();
                    var myMobileNorm = NormalizeMobile(myMobile);
                    var studentPhone = request.Student?.phone;
                    owns = studentPhone != null &&
                           ((!string.IsNullOrWhiteSpace(myMobile) && studentPhone == myMobile) ||
                            (myMobileNorm != null && studentPhone == myMobileNorm));
                }
                if (!owns)
                    throw UserFriendlyException.NotFound("الطلب غير موجود");
            }

            var history = await _workflow.GetHistoryAsync(requestId);

            return new MyRequestDetailDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                Status = request.Status,
                RegistrationData = request.RegistrationData,
                SubmittedAt = request.SubmittedAt,
                ReviewedAt = request.ReviewedAt,
                Notes = request.Notes,
                Student = request.Student == null ? null : new MyRequestStudentDto
                {
                    student_id = request.Student.student_id,
                    full_name = request.Student.full_name,
                    national_id = request.Student.national_id,
                    college = request.Student.college,
                    department = request.Student.department,
                    phone = request.Student.phone,
                    ad_username = request.Student.ad_username
                },
                History = history.Select(h => new WorkflowHistoryItemDto
                {
                    FromStage = h.FromStage,
                    ToStage = h.ToStage,
                    ActionDate = h.ActionDate,
                    Notes = h.Notes,
                    ActorName = h.Actor != null
                        ? (h.Actor.UserRoles.Any(ur => ur.Role.Name == "user") && request.Student?.full_name != null ? request.Student.full_name : h.Actor.full_name ?? h.Actor.UserName)
                        : null
                }).ToList()
            };
        }

        public async Task ResubmitAsync(int requestId, ResubmitRequest request)
        {
            var actorId = RequireActor();

            var result = await _registration.ResubmitRequestAsync(requestId, actorId, request.RegistrationData);
            if (result == null)
                throw new UserFriendlyException("لا يمكن إعادة تقديم هذا الطلب", 400);

            var (ip, ua) = ClientInfo();
            await _workflow.LogAuditAsync(actorId, "request_resubmitted", "Requests", requestId, ip, ua);
        }

        // ----------------------------- Helpers -----------------------------

        private static string? ExtractRegField(object? regData, string fieldName)
        {
            if (regData == null) return null;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(regData);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(fieldName, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
                    return prop.GetString();
            }
            catch { }
            return null;
        }
    }
}
