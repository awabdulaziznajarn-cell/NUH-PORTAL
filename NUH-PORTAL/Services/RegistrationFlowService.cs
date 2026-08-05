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

            // نص واحد للـ JSON يُستخدم في التحويل وفي الأرشيف معًا
            var registrationDataJson = RegistrationDataMapper.Serialize(request.RegistrationData);

            var student = await _students.FindAsync(s => s.student_id == request.StudentId);
            if (student == null)
            {
                // التحويل كله في RegistrationDataMapper.Apply — نفس الدالة التي
                // تستخدمها إعادة التقديم. أي حقل جديد يُضاف هناك مرة واحدة فيعمل
                // في المسارين، بدل نسختين تتفارقان مع أول تعديل.
                student = new Student
                {
                    student_id = request.StudentId,
                    status = StudentState.active,   // الطالب لا يحدّد حالة سكنه
                    created_at = DateTime.UtcNow,
                    created_by = actorId
                };
                RegistrationDataMapper.Apply(student, registrationDataJson);
                await _lookups.ApplyAsync(student); // FK ids من الأكواد (dual-write)
                await _students.AddAsync(student);
                await UnitOfWork.SaveAsync();
            }

            var mobile = RegistrationDataMapper.Read(registrationDataJson, "mobile")
                         ?? RegistrationDataMapper.Read(registrationDataJson, "phone");
            var nationalId = RegistrationDataMapper.Read(registrationDataJson, "national_id");

            // فحص واحد يغطّي الرقم الجامعي ورقم الهوية ورقم الجوال، ويرجّع رقم الطلب
            // المتعارض عشان الرسالة تكون مفيدة: الطالب يعرف يتابع طلبه بدل ما يحاول تاني.
            // ⚠️ الفحص يغطّي الثلاثة معًا: الرقم الجامعي، رقم الهوية، رقم الجوال.
            //    من غير كده كان الطالب يقدر يقدّم طلبًا ثانيًا بجوال مختلف وبنفس
            //    رقم الهوية أو الرقم الجامعي — والنتيجة سجلّان لنفس الشخص.
            var existing = await _registration.FindOpenRequestNumberAsync(request.StudentId, nationalId, mobile);
            if (existing != null)
                throw new UserFriendlyException(DuplicateMessage(existing, isOpen: true), 400);

            // الطالب المسجَّل بالفعل (طلب مكتمل وسكنه ساري) لا يقدّم طلبًا جديدًا.
            // الحالة النهائية (مغادرة/تخرّج/تحويل) تفتح له التسجيل تلقائيًا.
            var housed = await _registration.FindActiveHousingRequestNumberAsync(request.StudentId, nationalId, mobile);
            if (housed != null)
                throw new UserFriendlyException(DuplicateMessage(housed, isOpen: false), 400);

            var requestNumber = await _registration.GenerateRequestNumberAsync();

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
                // ⚠️ النوعين مع بعض. "طلبات الطالب" معناها كل طلبات سكنه، سواء قدّمها
                //    بنفسه (self_registration) أو قدّمها له موظف (housing) — هو ماختارش
                //    مين قدّمها. الاستثناء ده كان بيخلي نص طلباته غير مرئية له:
                //    مابيشوفهاش في "طلباتي"، وفحص التكرار في شاشة الجوال مابيلاقيهاش
                //    فيعدّي ويقدّم طلب تاني وهو عنده طلب مفتوح.
                .Where(r => r.RequestType == RequestType.self_registration
                         || r.RequestType == RequestType.housing);

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
                .FirstOrDefaultAsync(r => r.Id == requestId
                    && (r.RequestType == RequestType.self_registration || r.RequestType == RequestType.housing))
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            // ⚠️ الميثود دي كانت بتدّي أي رقم طلب لأي طالب مسجّل دخول — يعني تغيير
            //    الـ id في الرابط كان بيرجّع بيانات طالب تاني كاملة (رقم الهوية والجوال).
            //    الموظفين بيوصلوا عادي؛ الطالب لازم يكون صاحب الطلب.
            //    بنرجّع "غير موجود" مش "ممنوع" عشان مانأكّدش وجود الطلب أصلاً.
            var actorId = RequireActor();
            var isStaff = IsStaffRole(UnitOfWork.GetCurrentUserRole());
            if (!isStaff)
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
                // القراءة الوحيدة المسموحة من الأرشيف: إعادة تعبئة نموذج الطالب
                // عند التعديل. مطابق لسجل الطالب لأن المسارين يُكتبان معًا.
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
                    // ⚠️ ده رد بيروح للطالب. ملاحظات الموافقة ملاحظات داخلية بين
                    //    الموظفين — الطالب يشوف سبب الرفض والمطلوب استكماله بس.
                    //    الموظف بيشوف كل الملاحظات: نفس الـ endpoint بتستخدمه شاشة
                    //    تفاصيل الطلب عنده، ولو خفينا عنه ملاحظاته هو يبقى السجل ناقص.
                    Notes = (isStaff || IsStudentVisibleNote(h.ToStage)) ? h.Notes : null,
                    // ⚠️ اسم الموظف لا يخرج للطالب. الطالب يهمّه الجهة والتاريخ،
                    //    ولا مصلحة في أن يعرف اسم من راجع طلبه. الموظف يرى الأسماء
                    //    كاملة في شاشة تفاصيل الطلب (مسار مختلف ومحمي بالصلاحيات).
                    ActorName = !isStaff ? null
                        : (h.Actor != null
                            ? (h.Actor.UserRoles.Any(ur => ur.Role.Name == "user") && request.Student?.full_name != null
                                ? request.Student.full_name
                                : h.Actor.full_name ?? h.Actor.UserName)
                            : null)
                }).ToList()
            };
        }

        // ====================================================================
        //  ⚠️ عيب كان بيخلي المراجع يتخذ قرار على بيانات قديمة:
        //     إعادة التقديم كانت بتحدّث Requests.registration_data بس، وشاشة
        //     «تفاصيل الطلب» بتقرا من جدول Students (اللي بيتعمل مرة واحدة عند
        //     أول تقديم). فالطالب يعدّل رقم المبنى ويعيد التقديم، والمشرف يعمل
        //     تحديث فيلاقي القيم القديمة زي ما هي.
        //     الحل: نسحب التعديلات على سجل الطالب كمان، ونكتب ملخّص بالتغييرات
        //     في سجل المراجعات عشان المراجع يشوف الطالب غيّر إيه بالظبط.
        // ====================================================================
        public async Task ResubmitAsync(int requestId, ResubmitRequest request)
        {
            var actorId = RequireActor();

            // البيانات القديمة لازم تتقرا قبل ما إعادة التقديم تكتب فوقها
            var before = await _requests.Query().AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => new { r.RegistrationData, r.StudentId })
                .FirstOrDefaultAsync();

            // مقارنة واحدة تنتج النص المقروء والـ JSON المنظّم معًا
            var changes = RegistrationDataMapper.Compare(before?.RegistrationData, request.RegistrationData);
            var summary = RegistrationDataMapper.BuildChangeSummary(changes);
            var changesJson = RegistrationDataMapper.SerializeChanges(changes);

            var result = await _registration.ResubmitRequestAsync(
                requestId, actorId, request.RegistrationData, summary, changesJson);
            if (result == null)
                throw new UserFriendlyException("لا يمكن إعادة تقديم هذا الطلب", 400);

            if (before != null)
                await ApplyRegistrationDataToStudentAsync(before.StudentId, request.RegistrationData);

            var (ip, ua) = ClientInfo();
            await _workflow.LogAuditAsync(actorId, "request_resubmitted", "Requests", requestId, ip, ua);
        }

        // التحويل من بيانات التسجيل إلى سجل الطالب — نفس دالة التقديم الأول.
        // (المنطق كله في RegistrationDataMapper: مكان واحد لكل المسارات)
        private async Task ApplyRegistrationDataToStudentAsync(int studentId, string? registrationJson)
        {
            if (string.IsNullOrWhiteSpace(registrationJson)) return;

            var student = await _students.GetByIdAsync(studentId);
            if (student == null) return;

            RegistrationDataMapper.Apply(student, registrationJson);
            await _lookups.ApplyAsync(student);   // إعادة ربط FK من الأكواد الجديدة
            await UnitOfWork.SaveAsync();
        }

        // فحص مبكر من داخل النموذج — راحة للطالب فقط، وليس بديلًا عن فحص الإرسال.
        // يتطلب جلسة (رمز الطالب بعد التحقق بالجوال) ومحدود بمعدل في Program.cs.
        public async Task<DuplicateCheckResultDto> CheckDuplicateAsync(DuplicateCheckRequest request)
        {
            RequireActor();   // بدون جلسة متحقَّق منها لا فحص

            var hit = await _registration.FindByStudentAndNationalIdAsync(
                request.StudentId ?? string.Empty, request.NationalId ?? string.Empty);

            if (hit == null)
                return new DuplicateCheckResultDto { Found = false };

            return new DuplicateCheckResultDto
            {
                Found = true,
                RequestNumber = hit.RequestNumber,
                Message = $"هذه البيانات مرتبطة بطلب مسجَّل رقمه {hit.RequestNumber}. يرجى التأكد من صحتها أو مراجعة إدارة الإسكان."
            };
        }

        // ----------------------------- Helpers -----------------------------

        // رسالة التعارض تختلف حسب الحقل: تطابق الجوال يعني إن ده الطالب نفسه،
        // أما تطابق الهوية أو الرقم الجامعي فقد يعني إدخالًا خاطئًا أو محاولة
        // تسجيل ببيانات شخص آخر — والإرشاد في الحالتين مختلف تمامًا.
        private static string DuplicateMessage(DuplicateMatch m, bool isOpen)
        {
            if (m.IsSamePerson)
                return isOpen
                    ? $"لديك طلب قائم بالفعل رقمه {m.RequestNumber} — يمكنك متابعته من صفحة تتبع الطلب."
                    : $"أنت مسجَّل بالفعل في الإسكان الجامعي بموجب الطلب رقم {m.RequestNumber}. لا يمكن تقديم طلب جديد؛ للاستفسار يرجى مراجعة إدارة الإسكان.";

            return isOpen
                ? $"{m.FieldLabel} المُدخل مرتبط بطلب قائم رقمه {m.RequestNumber}. يرجى التأكد من صحة البيانات، وإن كان الطلب يخصّك فتابعه من صفحة تتبع الطلب."
                : $"{m.FieldLabel} المُدخل مرتبط بطالب مسجَّل بالفعل في الإسكان الجامعي (الطلب رقم {m.RequestNumber}). يرجى التأكد من صحة البيانات أو مراجعة إدارة الإسكان.";
        }


        // مسار تسجيل الطالب بيكتب "rejected"، ومسار الموظف housing_rejected /
        // cyber_rejected — الاتنين رفض.
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

    }
}
