using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
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
        private readonly IPledgeService _pledge;
        private readonly IRegistrationService _registration;
        private readonly IWorkflowService _workflow;
        private readonly IHttpContextAccessor _http;
        private readonly ILookupResolver _lookups;
        private readonly UserManager<User> _userManager;

        public RegistrationFlowService(
            IRepository<Student> students,
            IRepository<User> users,
            IRepository<Request> requests,
            IRepository<StudentDeclaration> declarations,
            IPledgeService pledge,
            IRegistrationService registration,
            IWorkflowService workflow,
            IHttpContextAccessor http,
            ILookupResolver lookups,
            UserManager<User> userManager,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _users = users;
            _requests = requests;
            _declarations = declarations;
            _pledge = pledge;
            _registration = registration;
            _workflow = workflow;
            _http = http;
            _lookups = lookups;
            _userManager = userManager;
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
        // ⚠️ حُذفت النسخة المحلية — القاعدة الوحيدة في Core/IdentityRules.cs.
        private static string? NormalizeMobile(string? mobile) => IdentityRules.NormalizeMobile(mobile);

        private int RequireActor()
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);
            return actorId;
        }

        // ====================================================================
        //  ملكية الطلب — القاعدة الوحيدة، وكل مسار بيلمس طلب بيعدّي منها.
        //
        //  ⚠️ الفحص ده كان مكتوب جوّه GetMyRequestDetailAsync بس. باقي المسارات
        //     اللي بتاخد requestId من العميل كانت بتتحقق إن الطلب *موجود* وخلاص:
        //
        //       • ResubmitAsync      → أي طالب يعيد تقديم طلب أي طالب تاني،
        //                              والبيانات الجديدة بتتكتب فوق سجل الضحية.
        //       • AcceptDeclarations → توقيع تعهّد قانوني على طلب حد تاني،
        //                              والـ IP المسجّل في الإقرار بيبقى بتاع المهاجم.
        //
        //     ورقم الطلب عدد متسلسل، يعني التجربة بالترتيب سهلة.
        //
        //  ⚠️ الملكية بتتحدد بطريقتين مش واحدة: SubmittedBy لما الطالب يقدّم
        //     بنفسه، والمطابقة بالجوال لما المشرف يقدّم نيابةً عنه (ساعتها
        //     SubmittedBy حساب المشرف مش الطالب). لو اكتفينا بالأولى، الطالب
        //     اللي سجّله المشرف مش هيقدر يكمّل طلبه.
        //
        //  الموظفون بيعدّوا — صلاحياتهم متفحوصة على مستوى الكنترولر.
        //  وبنرمي "غير موجود" مش "ممنوع": مانأكّدش وجود الطلب أصلًا.
        // ====================================================================
        private async Task EnsureOwnsRequestAsync(int actorId, Request? request)
        {
            if (request == null)
                throw UserFriendlyException.NotFound("الطلب غير موجود");

            if (IsStaffRole(UnitOfWork.GetCurrentUserRole()))
                return;

            if (request.SubmittedBy == actorId)
                return;

            var myMobile = await _users.Query().AsNoTracking()
                .Where(u => u.Id == actorId)
                .Select(u => u.mobile)
                .FirstOrDefaultAsync();

            var studentPhone = request.StudentId == 0 ? null : await _students.Query().AsNoTracking()
                .Where(st => st.Id == request.StudentId)
                .Select(st => st.phone)
                .FirstOrDefaultAsync();

            var myMobileNorm = NormalizeMobile(myMobile);
            var owns = studentPhone != null &&
                       ((!string.IsNullOrWhiteSpace(myMobile) && studentPhone == myMobile) ||
                        (myMobileNorm != null && studentPhone == myMobileNorm));

            if (!owns)
                throw UserFriendlyException.NotFound("الطلب غير موجود");
        }

        public async Task<StartRegistrationResultDto> StartAsync(StartRegistrationRequest request)
        {
            var actorId = RequireActor();

            // نص واحد للـ JSON يُستخدم في التحويل وفي الأرشيف معًا
            var registrationDataJson = RegistrationDataMapper.Serialize(request.RegistrationData);

            var mobile = RegistrationDataMapper.Read(registrationDataJson, "mobile")
                         ?? RegistrationDataMapper.Read(registrationDataJson, "phone");
            var nationalId = RegistrationDataMapper.Read(registrationDataJson, "national_id");

            // ================================================================
            //  الفحوص قبل إنشاء سجل الطالب — الترتيب ده مش تفصيلة.
            //
            //  ⚠️ كان سجل الطالب بيتحفظ *الأول*، وبعده تيجي فحوص التكرار وترمي
            //     ٤٠٠. والنتيجة صفّ طالب يتيم: اتحفظ ومفيش طلب مربوط بيه، ومحدش
            //     بيحذفه. ورقم الهوية عليه فهرس فريد — يعني **صاحب الهوية
            //     الحقيقي بقى مستحيل يتسجّل بعد كده**، وكل محاولة بترجع «رقم
            //     الهوية موجود بالفعل» وهو مش موجود في أي طلب.
            //
            //  ⚠️ والفحوص دي ماكانتش محتاجة السجل أصلًا: بتاخد الرقم الجامعي
            //     ورقم الهوية والجوال من الحمولة مباشرة. فترتيبهم بعد الحفظ
            //     ماكانش له سبب — كان سهو.
            // ================================================================

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

            // ⚠️ ومعاملة واحدة تلفّ الباقي: إنشاء الطالب وتوليد رقم الطلب وإنشاء
            //    الطلب. الترتيب فوق شال أشهر سبب لليُتم، والمعاملة بتقفل الباقي —
            //    تصادم في رقم الطلب، أو انقطاع في النص. يا الاتنين يا ولا واحد.
            using var transaction = await UnitOfWork.BeginTransactionAsync();
            try
            {

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
                    // ⚠️ الوضع الأكاديمي كان يُترك فارغًا في هذا المسار وحده، بينما
                    //    يضبطه StudentService (تسجيل فردي) و BulkRegistrationService
                    //    (رفع إكسل). فالطالب المسجَّل ذاتيًا يظهر في قائمة الطلاب
                    //    بعمود «الوضع الأكاديمي» فارغًا، وتعرض الشاشة مفتاح الترجمة
                    //    الناقص نصًّا خامًا. المسارات الثلاثة الآن تبدأ من الحالة نفسها.
                    student_status = StudentStatus.active,
                    created_at = DateTime.UtcNow,
                    created_by = actorId
                };
                RegistrationDataMapper.Apply(student, registrationDataJson);
                await _lookups.ApplyAsync(student); // FK ids من الأكواد (dual-write)
                await _students.AddAsync(student);
                await UnitOfWork.SaveAsync();
            }

            // ⚠️ حساب دخول الطالب يُنشأ لحظة التحقق برمز الجوال، أي قبل أن يوجد
            //    له سجل طالب. فيولد باسم مؤقت مبني على رقم جواله واسمه «طالب».
            //    وبعد هذه اللحظة صار الرقم الجامعي والاسم الكامل معروفين — ومن
            //    غير هذا السطر يظل الحساب على شكله المؤقت حتى الدخول التالي،
            //    فيظهر جدول واحد بصيغتين لاسم المستخدم وباسم «طالب» لحسابات
            //    نعرف أصحابها. القاعدة واحدة في StudentLoginIdentity.
            //    ⚠️ الشرط ليس تجميلًا: المشرف قد يقدّم الطلب نيابةً عن الطالب،
            //    وحينها actorId حساب المشرف — ومزامنته ببيانات الطالب تعيد تسمية
            //    حساب موظف باسم طالب. لا نزامن إلا إذا كان جوال المُقدِّم هو جوال
            //    الطالب نفسه، أي أن الحساب حساب الطالب فعلًا.
            var actor = await _userManager.FindByIdAsync(actorId.ToString());
            var actorMobile = NormalizeMobile(actor?.mobile);
            if (actor != null && actorMobile != null && actorMobile == NormalizeMobile(student.phone))
                await StudentLoginIdentity.SyncAsync(_userManager, actor, student, actorMobile);

            var requestNumber = await _registration.GenerateRequestNumberAsync();

            var newRequest = await _registration.CreateRegistrationRequestAsync(
                student.Id, requestNumber, registrationDataJson, actorId);

            var (ip, ua) = ClientInfo();
            await _workflow.LogAuditAsync(actorId, "registration_created", "Requests", newRequest.Id, ip, ua);

            // ⚠️ التعهّد جوّه نفس المعاملة. لو الجملة مش مطابقة أو البنود
            //    اتغيّرت، الاستثناء بيطلع من هنا فالمعاملة بترجع بالكامل —
            //    يعني مفيش طلب اتخلق بلا تعهّد، ولا سجل طالب اتساب وراه.
            //    الشرح الكامل في Core/PledgeRules.cs و DTOs/Registration.
            await SavePledgeAsync(newRequest.Id, request.Pledge, actorId, ip, ua);

            await transaction.CommitAsync();

            return new StartRegistrationResultDto
            {
                Message = "تم تقديم طلب التسجيل بنجاح",
                RequestId = newRequest.Id,
                RequestNumber = newRequest.RequestNumber
            };

            }
            catch
            {
                // أي فشل بعد هنا = مفيش سجل طالب متسيّب وراه.
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ⚠️ تفويض لا نسخة: البناء نفسه في Services/PledgeService.cs عشان
        //    شاشة تفاصيل الطلب تقرا من نفس المكان. موجودة هنا لأن الواجهة
        //    بتناديها على /api/Registration/pledge وده مسار التسجيل.
        public Task<PledgeDocumentDto> GetPledgeDocumentAsync() => _pledge.GetDocumentAsync();

        // ====================================================================
        //  حفظ التعهّد — المسار الوحيد اللي بيكتب في StudentDeclarations.
        //
        //  ⚠️ كل اللي بيتاخد من العميل هنا: علامتَي الموافقة والجملة المكتوبة
        //     والبصمة اللي كان شايفها. النصّ والبصمة والنسخة بيتبنوا على
        //     الخادم من الجدول — العميل ماعندوش أي طريقة يقول بيها «وافقت
        //     على بنود غير دي».
        //
        //  ⚠️ والرفض هنا بيرمي UserFriendlyException وسط المعاملة، يعني
        //     الطلب كله بيترجع. تعهّد مرفوض = مفيش طلب أصلًا، مش طلب بلا
        //     تعهّد.
        // ====================================================================
        private async Task SavePledgeAsync(
            int requestId, AcceptDeclarationsRequest? pledge, int actorId, string? ip, string ua)
        {
            if (pledge == null || !pledge.DeclarationAccepted || !pledge.PolicyAccepted)
                throw new UserFriendlyException(PledgeRules.NotAcceptedError, 400);

            // ⚠️ الفحص ده على الخادم مش زيادة على فحص الواجهة: أي حد يقدر يبعت
            //    الطلب من غير ما يفتح الصفحة أصلًا.
            if (!PledgeRules.SentenceMatches(pledge.TypedConfirmation))
                throw new UserFriendlyException(PledgeRules.SentenceError, 400);

            var doc = await _pledge.GetDocumentAsync();

            // بنود فاضية = مفيش تعهّد أصلًا. نرفض بدل ما نخزّن موافقة على لا شيء.
            if (doc.Items.Count == 0)
                throw new UserFriendlyException(PledgeRules.NoTermsError, 503);

            // ⚠️ البصمة اللي الطالب شافها لازم تساوي اللي الخادم بناها دلوقتي.
            //    غير كده يبقى المدير عدّل البنود وهو بيملا الطلب — فنوقّفه
            //    ونقوله يقرا من جديد، لا نسجّل موافقته على نصّ ماشافهوش.
            if (!string.IsNullOrWhiteSpace(pledge.TermsHash) &&
                !string.Equals(pledge.TermsHash, doc.Hash, StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException(PledgeRules.TermsChangedError, 409);

            var declaration = new StudentDeclaration
            {
                RequestId = requestId,
                DeclarationAccepted = true,
                PolicyAccepted = true,
                PolicyVersion = doc.Version,
                TermsText = doc.Text,
                TermsHash = doc.Hash,
                // كما كتبها بالحرف — التطبيع كان للمقارنة بس.
                TypedConfirmation = pledge.TypedConfirmation!.Trim(),
                AcceptedDate = DateTime.UtcNow,
                IPAddress = ip,
                UserAgent = ua
            };

            await _declarations.AddAsync(declaration);
            await UnitOfWork.SaveAsync();

            await _workflow.LogAuditAsync(actorId, "declaration_accepted", "StudentDeclarations", declaration.Id, ip, ua);
        }

        // ====================================================================
        //  المسار المنفصل: تعهّد على طلب قائم.
        //
        //  ⚠️ باقي موجود مع إن التسجيل الذاتي بقى بيبعت التعهّد مع /start:
        //     الطلب اللي بيقدّمه المشرف نيابةً عن الطالب بيتعمل من شاشة تانية،
        //     والطالب بيوقّع تعهّده بعدين. المنطق واحد — نفس SavePledgeAsync.
        //
        //  ⚠️ ومفيش تعهّدين لطلب واحد: التاني بيترفض بدل ما يتراكم في الجدول
        //     فمحدش يعرف أنهي واحد هو المعتمد.
        // ====================================================================
        public async Task AcceptDeclarationsAsync(int requestId, AcceptDeclarationsRequest request)
        {
            var actorId = RequireActor();

            var declRequest = await _requests.GetByIdAsync(requestId);
            await EnsureOwnsRequestAsync(actorId, declRequest);

            if (await _declarations.Query().AsNoTracking().AnyAsync(d => d.RequestId == requestId))
                throw new UserFriendlyException("تم توقيع التعهّد لهذا الطلب من قبل.", 409);

            var (ip, ua) = ClientInfo();
            await SavePledgeAsync(requestId, request, actorId, ip, ua);
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
            //    الفحص نفسه اللي بتستخدمه إعادة التقديم والإقرارات — قاعدة واحدة.
            await EnsureOwnsRequestAsync(RequireActor(), request);

            // ⚠️ محتاجينها تحت كمان: ملاحظات المراجعة واسم الموظف بيظهروا للموظف
            //    بس، والطالب بيشوف سبب الرفض والمطلوب استكماله فقط.
            var isStaff = IsStaffRole(UnitOfWork.GetCurrentUserRole());

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
                // الخانات اللي المراجع طلب تصحيحها. فاضية = كل الخانات مفتوحة —
                // وده حال أي طلب اترجّع قبل ما الميزة دي تتعمل.
                InfoFields = string.IsNullOrWhiteSpace(request.InfoFields)
                    ? new List<string>()
                    : request.InfoFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
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

            // ⚠️ الملكية الأول: من غيرها كان أي طالب يعيد تقديم طلب أي طالب تاني
            //    والبيانات الجديدة تتكتب فوق سجل الضحية.
            await EnsureOwnsRequestAsync(actorId, await _requests.GetByIdAsync(requestId));

            // البيانات القديمة لازم تتقرا قبل ما إعادة التقديم تكتب فوقها
            var before = await _requests.Query().AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => new { r.RegistrationData, r.StudentId, r.InfoFields })
                .FirstOrDefaultAsync();

            // مقارنة واحدة تنتج النص المقروء والـ JSON المنظّم معًا
            var changes = RegistrationDataMapper.Compare(before?.RegistrationData, request.RegistrationData);

            // ================================================================
            //  ⚠️ قفل الخانات لازم يكون هنا، مش في المتصفح.
            //
            //  إعادة التقديم بتكتب RegistrationData بالكامل بأي حاجة العميل
            //  يبعتها. يعني طالب اترجّعله الطلب عشان «رقم المبنى» كان يقدر
            //  يغيّر رقم هويته أو اسمه ويعيد التقديم — والمراجع يشوف الطلب
            //  راجع ويوافق عليه وهو مش واخد باله إن الهوية اتغيّرت بعد ما
            //  راجعها. القفل في الواجهة ديكور: أي حد يفتح أدوات المطوّر بيعدّيه.
            //
            //  القاعدة: المراجع حدد خانات؟ اللي برّاها مايتغيّرش. ماحددش؟
            //  كل حاجة مفتوحة (سلوك الطلبات القديمة زي ما هو).
            // ================================================================
            // ⚠️ قفل دائم لا يتبع اختيار المراجع: الرقم الجامعي هوية الطلب.
            //    الفحص اللي تحته بيشتغل بس لو المراجع حدد خانات — والطلبات
            //    المرجّعة قبل ميزة التحديد InfoFields فيها فاضية، فكل خاناتها
            //    مفتوحة ومنها الرقم الجامعي. طالب يغيّره ساعتها يبقى الطلب
            //    اتراجع لواحد واترجّع باسم واحد تاني.
            var idChange = changes.FirstOrDefault(c =>
                string.Equals(RegistrationDataMapper.NormalizeFieldKey(c.Field), "student_id",
                              StringComparison.OrdinalIgnoreCase));
            if (idChange != null)
                throw new UserFriendlyException(
                    "لا يمكن تعديل الرقم الجامعي بعد تقديم الطلب. " +
                    "لو الرقم غير صحيح، قدّم طلبًا جديدًا بالرقم الصحيح.", 400);

            var allowed = string.IsNullOrWhiteSpace(before?.InfoFields)
                ? null
                : before!.InfoFields!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(RegistrationDataMapper.NormalizeFieldKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (allowed != null)
            {
                var blocked = changes
                    .Where(c => !allowed.Contains(RegistrationDataMapper.NormalizeFieldKey(c.Field)))
                    .Select(c => RegistrationDataMapper.LabelOf(c.Field))
                    .Distinct()
                    .ToList();

                if (blocked.Count > 0)
                    throw new UserFriendlyException(
                        "لا يمكن تعديل: " + string.Join("، ", blocked) +
                        ". المطلوب تصحيحه فقط: " +
                        string.Join("، ", allowed.Select(RegistrationDataMapper.LabelOf)) + ".", 400);
            }

            var summary = RegistrationDataMapper.BuildChangeSummary(changes);
            var changesJson = RegistrationDataMapper.SerializeChanges(changes);

            // ملاحظة الطالب بتتضاف لملخّص التعديلات فبتوصل للمراجع في سجل المسار
            if (!string.IsNullOrWhiteSpace(request.StudentNote))
                summary = (string.IsNullOrWhiteSpace(summary) ? "" : summary + " - ")
                        + "ملاحظة الطالب: " + request.StudentNote.Trim();

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

            // ⚠️ رقم الطلب لا يخرج من هنا. هذا الفحص يطابق على الرقم الجامعي
            //    ورقم الهوية فقط — ولا يثبت أن من أمام الشاشة هو صاحب الطلب.
            //    من يكتب بيانات شخص آخر (خطأً أو قصدًا) كان يحصل على رقم طلبه،
            //    ورقم الطلب مع آخر ٤ أرقام من الجوال يفتح شاشة التتبع.
            //    الرقم يُذكر فقط حين يثبت التطابق بالجوال المتحقَّق منه بالـ OTP.
            return new DuplicateCheckResultDto
            {
                Found = true,
                Message = "هذه البيانات مرتبطة بطلب مسجَّل. يرجى التأكد من صحتها أو مراجعة إدارة الإسكان."
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
                    ? $"لديك طلب قائم بالفعل رقمه {m.RequestNumber} - يمكنك متابعته من صفحة تتبع الطلب."
                    : $"أنت مسجَّل بالفعل في الإسكان الجامعي بموجب الطلب رقم {m.RequestNumber}. لا يمكن تقديم طلب جديد؛ للاستفسار يرجى مراجعة إدارة الإسكان.";

            // التطابق على الهوية أو الرقم الجامعي لا يثبت الملكية، فلا يُذكر رقم
            // الطلب. صاحب الطلب الحقيقي يصل إليه من شاشة التحقق بجواله.
            return isOpen
                ? $"{m.FieldLabel} المُدخل مرتبط بطلب قائم. يرجى التأكد من صحة البيانات، وإن كان الطلب يخصّك فتابعه من صفحة تتبع الطلب بعد التحقق برقم جوالك."
                : $"{m.FieldLabel} المُدخل مرتبط بطالب مسجَّل بالفعل في الإسكان الجامعي. يرجى التأكد من صحة البيانات أو مراجعة إدارة الإسكان.";
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
