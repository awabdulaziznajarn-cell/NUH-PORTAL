using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Registration;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // ========================================================================
    //  وثيقة التعهّد. القواعد (التطبيع، البصمة، شكل النصّ) في
    //  Core/PledgeRules.cs - الخدمة دي بتجيب البيانات وبس.
    // ========================================================================
    public class PledgeService : IPledgeService
    {
        private readonly IRepository<Term> _terms;
        private readonly IRepository<StudentDeclaration> _declarations;
        private readonly IRepository<Request> _requests;
        private readonly IConfiguration _config;
        private readonly IAuditService _audit;
        private readonly IUnitOfWork _uow;

        public PledgeService(
            IRepository<Term> terms,
            IRepository<StudentDeclaration> declarations,
            IRepository<Request> requests,
            IConfiguration config,
            IAuditService audit,
            IUnitOfWork uow)
        {
            _terms = terms;
            _declarations = declarations;
            _requests = requests;
            _config = config;
            _audit = audit;
            _uow = uow;
        }

        // ====================================================================
        //  مفتاح توقيع رمز الوثيقة.
        //
        //  ⚠️ أجيال في قاموس لا مفتاح واحد: أوراق اتطبعت السنة اللي فاتت لازم
        //     تفضل قابلة للتحقّق بعد ما المفتاح يتبدّل. الرمز شايل رقم جيله في
        //     أول خانة، والتحقّق بيدوّر على مفتاح الجيل ده تحديدًا.
        //
        //  ⚠️ ومفيش مفتاح افتراضي في الكود. مفتاح مكتوب في الكود معناه إن أي
        //     حد شاف المستودع يقدر يولّد أوراق «صحيحة» - وde أسوأ من مفيش رمز
        //     خالص، لأن الورقة ساعتها بتوحي بتحقّق مش موجود. المفتاح ناقص ⇒
        //     الوثيقة بتتطبع بلا رمز.
        // ====================================================================
        private int CurrentKeyGeneration => _config.GetValue("Pledge:KeyGeneration", 1);

        private string? KeyFor(int generation) => _config["Pledge:SigningKeys:" + generation];

        // ⚠️ IsActive بس، والترتيب DisplayOrder ثم Id - نفس شرط وترتيب
        //    LookupService.GetTermsAsync اللي شاشة القوائم بتعرض بيه. لو
        //    اتفارقوا، الطالب هيشوف ترتيبًا والبصمة تتحسب على ترتيب تاني.
        public async Task<PledgeDocumentDto> GetDocumentAsync()
        {
            var terms = await _terms.Query().AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id)
                .Select(t => new { t.Id, t.DisplayOrder, t.ArText, t.EnText })
                .ToListAsync();

            var text = PledgeRules.BuildTermsText(
                terms.Select(t => (t.DisplayOrder, t.Id, t.ArText)));

            var items = new List<PledgeTermItemDto>();
            var n = 0;
            foreach (var t in terms)
            {
                if (string.IsNullOrWhiteSpace(t.ArText)) continue;
                n++;
                items.Add(new PledgeTermItemDto
                {
                    Order = n,
                    Ar = t.ArText.Trim(),
                    // البند مالوش ترجمة؟ نعرض العربي - أحسن من بند فاضي.
                    En = string.IsNullOrWhiteSpace(t.EnText) ? t.ArText.Trim() : t.EnText.Trim()
                });
            }

            return new PledgeDocumentDto
            {
                Items = items,
                Text = text,
                Hash = PledgeRules.ComputeHash(text),
                Version = PledgeRules.VersionOf(text, items.Count),
                RequiredSentence = PledgeRules.RequiredSentence
            };
        }

        // ⚠️ الترقيم بيتشال من أول كل سطر عشان الشاشة ترقّم بالـ CSS. النصّ
        //    المخزّن بيفضل زي ما هو - الشيل ده للعرض بس، والبصمة اتحسبت على
        //    النصّ الكامل بترقيمه.
        private static readonly Regex LineNumberRx =
            new(@"^\s*\d+\s*[\.\-\)]\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public async Task<PledgeRecordDto?> GetForRequestAsync(int requestId)
        {
            // ⚠️ الأحدث لو اتكرّر: المسار الطبيعي بيمنع التكرار، بس الطلب
            //    القديم اللي اتوقّع من مسار المشرف ممكن يكون فيه أكتر من صفّ
            //    من قبل المنع ده. الأحدث هو المعتمد.
            var d = await _declarations.Query().AsNoTracking()
                .Where(x => x.RequestId == requestId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            if (d == null) return null;

            var lines = string.IsNullOrWhiteSpace(d.TermsText)
                ? new List<string>()
                : d.TermsText.Split('\n')
                    .Select(l => LineNumberRx.Replace(l, string.Empty).Trim())
                    .Where(l => l.Length > 0)
                    .ToList();

            // ⚠️ المقارنة دي هي فايدة البصمة كلها: هل البنود المعمول بها
            //    دلوقتي هي نفسها اللي الطالب وافق عليها؟ من غيرها المراجع
            //    بيفتح شاشة البنود ويفتكر إن اللي فيها هو اللي اتوافق عليه.
            var changed = false;
            if (!string.IsNullOrWhiteSpace(d.TermsHash))
            {
                var current = await GetDocumentAsync();
                changed = !string.Equals(d.TermsHash, current.Hash, StringComparison.OrdinalIgnoreCase);
            }

            return new PledgeRecordDto
            {
                AcceptedAt = d.AcceptedDate,
                PolicyVersion = d.PolicyVersion,
                TermsHash = d.TermsHash,
                TypedConfirmation = d.TypedConfirmation,
                Terms = lines,
                IpAddress = d.IPAddress,
                TermsChanged = changed,
                // ⚠️ تعهّد قديم بلا نصّ محفوظ. الشاشة لازم تفرّق بينه وبين
                //    التعهّد الموثّق: الأول بيقول «وافق» والتاني بيقول «وافق
                //    على إيه» - وفرق كبير قدام أي جهة تدقيق.
                Documented = !string.IsNullOrWhiteSpace(d.TermsText)
                          && !string.IsNullOrWhiteSpace(d.TermsHash),

                // ⚠️ بيتحسب هنا مش بيتخزّن: كل مدخلاته موجودة في السجل أصلًا،
                //    وعمود مخزَّن معناه قيمة ممكن تفارق مصدرها بعد أي تعديل.
                DocCode = PledgeRules.BuildDocCode(
                    requestId, d.TermsHash, d.AcceptedDate,
                    KeyFor(CurrentKeyGeneration), CurrentKeyGeneration),

                RequestId = requestId
            };
        }

        // ====================================================================
        //  تسجيل طباعة الوثيقة، وإرجاع تاريخ الطباعة **من الخادم**.
        //
        //  ⚠️ التاريخ كان بيتاخد من `new Date()` في المتصفح - يعني من ساعة
        //     جهاز اللي بيطبع. على ورقة عادية مش مشكلة؛ على ورقة بتدخل محضر
        //     تحقيق، ده سطر بيقول تاريخًا والنظام مش شاهد عليه، وأي حد يقدر
        //     يغيّر ساعة جهازه ويطبع بتاريخ تاني.
        //
        //  ⚠️ وبترجّع **نصًّا** لا DateTime عن قصد: القيمة بتتطبع زي ما هي.
        //     لو رجّعناها تاريخًا، المتصفح هو اللي هيقرّر المنطقة الزمنية -
        //     وبنبقى رجّعنا نفس المشكلة من باب تاني.
        //
        //  ⚠️ والتسجيل في سجل العمليات جزء من نفس النداء لا نداء تاني: نداءان
        //     معناهما إن واحد ممكن ينجح والتاني يفشل، فتطلع ورقة عليها تاريخ
        //     بلا أثر في السجل - وهي بالظبط الحالة اللي بنعالجها.
        // ====================================================================
        public async Task<string?> RecordPrintAsync(int requestId)
        {
            var exists = await _declarations.Query().AsNoTracking()
                .AnyAsync(x => x.RequestId == requestId);

            // ⚠️ مفيش تعهّد ⇒ مفيش ورقة تتطبع ⇒ مفيش سجل. من غير الفحص ده
            //    النداء بيبقى باب لكتابة سجلات طباعة لطلبات مالهاش وثيقة أصلًا.
            if (!exists) return null;

            // ⚠️ "Requests" لا "StudentDeclarations": الرقم اللي بيتكتب هنا رقم
            //    الطلب. كان الاسم بيقول جدول والرقم بتاع جدول تاني، فسطر السجل
            //    بيوصّل لصفّ غلط لو حد اتبعه - وسجل بيوصّل لصفّ غلط أسوأ من
            //    سجل بلا هدف.
            await _audit.LogAsync("pledge_printed", "Requests", requestId);
            await _uow.SaveAsync();

            return CalendarFormat.DateTimeText(KsaTime.Now);
        }

        // ====================================================================
        //  التحقّق من رمز مطبوع على ورقة.
        //
        //  ⚠️ null واحدة لكل أسباب الفشل - رمز مش مقروء، رقم طلب مش موجود،
        //     تعهّد بلا بصمة، مفتاح جيل مش موجود، بصمة ما طابقتش. أي تفريق
        //     بينهم بيحوّل الصفحة لأداة استعلام: اللي بيجرّب رموز بيعرف من
        //     اختلاف الرد إن رقم الطلب ده موجود فعلًا.
        // ====================================================================
        // بتقرا الرمز وترجّع (التعهّد + الطلب) لو طابق، أو null لأي سبب.
        private async Task<(StudentDeclaration Decl, Request Req)?> ReadDocCodeAsync(string? code)
        {
            if (!PledgeRules.TryParseDocCode(code, out var generation, out var requestId, out var mac))
                return null;

            var d = await _declarations.Query().AsNoTracking()
                .Where(x => x.RequestId == requestId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            if (d == null || string.IsNullOrWhiteSpace(d.TermsHash)) return null;

            if (!PledgeRules.DocCodeMacMatches(
                    mac, generation, requestId, d.TermsHash, d.AcceptedDate, KeyFor(generation)))
                return null;

            var r = await _requests.Query().AsNoTracking()
                .Include(x => x.Student)
                .FirstOrDefaultAsync(x => x.Id == requestId);

            return r == null ? null : (d, r);
        }

        private static int TermsCountOf(StudentDeclaration d) =>
            string.IsNullOrWhiteSpace(d.TermsText)
                ? 0
                : d.TermsText.Split('\n').Count(l => l.Trim().Length > 0);

        public async Task<PledgeVerifyDto?> VerifyDocumentAsync(string? code)
        {
            var hit = await ReadDocCodeAsync(code);
            if (hit == null) return null;
            var (d, r) = hit.Value;

            return new PledgeVerifyDto
            {
                RequestNumber = r.RequestNumber,
                // ⚠️ الإخفاء من Core/PublicMasking - نفس قاعدة شاشة تتبّع الطلب.
                //    الكائن ده بيروح لصفحة مفتوحة للعالم، فمفيش نسخة كاملة
                //    بتخرج منه أصلًا. اللي محتاج الكامل بيستعمل ResolveDocCodeAsync.
                StudentName = PublicMasking.Name(r.Student?.full_name),
                StudentIdTail = PublicMasking.IdTail(r.Student?.student_id),
                AcceptedAt = d.AcceptedDate,
                PolicyVersion = d.PolicyVersion,
                TermsCount = TermsCountOf(d)
            };
        }

        public async Task<PledgeDocRefDto?> ResolveDocCodeAsync(string? code)
        {
            var hit = await ReadDocCodeAsync(code);
            if (hit == null) return null;
            var (d, r) = hit.Value;

            return new PledgeDocRefDto
            {
                RequestId = r.Id,
                StudentId = r.StudentId,
                RequestNumber = r.RequestNumber,
                AcceptedAt = d.AcceptedDate,
                PolicyVersion = d.PolicyVersion,
                TermsCount = TermsCountOf(d)
            };
        }
    }
}
