using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.DTOs.Registration;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // ========================================================================
    //  وثيقة التعهّد. القواعد (التطبيع، البصمة، شكل النصّ) في
    //  Core/PledgeRules.cs — الخدمة دي بتجيب البيانات وبس.
    // ========================================================================
    public class PledgeService : IPledgeService
    {
        private readonly IRepository<Term> _terms;
        private readonly IRepository<StudentDeclaration> _declarations;

        public PledgeService(IRepository<Term> terms, IRepository<StudentDeclaration> declarations)
        {
            _terms = terms;
            _declarations = declarations;
        }

        // ⚠️ IsActive بس، والترتيب DisplayOrder ثم Id — نفس شرط وترتيب
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
                    // البند مالوش ترجمة؟ نعرض العربي — أحسن من بند فاضي.
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
        //    المخزّن بيفضل زي ما هو — الشيل ده للعرض بس، والبصمة اتحسبت على
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
                //    على إيه» — وفرق كبير قدام أي جهة تدقيق.
                Documented = !string.IsNullOrWhiteSpace(d.TermsText)
                          && !string.IsNullOrWhiteSpace(d.TermsHash)
            };
        }
    }
}
