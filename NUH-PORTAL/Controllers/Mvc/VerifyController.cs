using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Core;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers.Mvc
{
    // ========================================================================
    //  التحقّق من وثيقة تعهّد مطبوعة.
    //
    //  ⚠️ الصفحة دي هي **كل** قيمة الرمز المطبوع. الورقة اللي عليها رمز بلا
    //     صفحة تقرأه بتبقى زي البصمة القديمة بالظبط: سطر جميل مالوش مرجع.
    //
    //  ⚠️ مفتوحة بلا تسجيل دخول عن قصد، وجمهورها **جوّه الشبكة** لا برّه:
    //     النظام كله داخلي، فاللي بيوصل للصفحة دي هو اللي على شبكة الإسكان
    //     ومالوش حساب في البوابة - وأولهم **الطالب نفسه**، صاحب الورقة، اللي
    //     مايقدرش يفتح /StudentFile لأنه ملوش حساب أصلًا. وبعده موظف على شبكة
    //     الجامعة بلا صلاحية InvestigateStudents.
    //     كان مكتوب هنا قبل كده إن الجمهور «جهة توظيف وولي أمر» - وده كلام
    //     مش صحيح في نظام داخلي: الجهة الخارجية ما بتوصلش للدومين من أساسه.
    //     لو اتقرّر يومًا إن التحقّق الخارجي مطلوب، ده قرار نشر وأمن سيبراني
    //     (فتح هذا المسار وحده للخارج) لا تعديل في الصفحة.
    //
    //  ⚠️ والمقابل إن اللي بتعرضه أقل ما يكفي للمطابقة - اسم مقنّع وآخر ٤
    //     أرقام (Core/PublicMasking) - والحدّ على المحاولات في Program.cs
    //     (١٢ محاولة في الدقيقة) جزء من الأمان لا زيادة.
    //
    //  ⚠️ ومفيش مسار API مقابل: باب تاني لنفس التحقّق معناه حدّ تاني لازم
    //     يفضل مظبوط، وأول ما يتنسي بيبقى هو الباب المفتوح.
    //
    //  ⚠️ والموظف اللي بيمسح الرمز وهو مسجّل دخول مابيقفش هنا: بيتحوّل على
    //     «ملف الطالب» والرمز مُعبَّى، فيلاقي الملف كامل بدل ستة صفوف مقنّعة.
    //     الصفحة دي للي **مالوش** حساب - وهو جمهورها الحقيقي.
    // ========================================================================
    [AllowAnonymous]
    [Route("Verify")]
    public class VerifyController : Controller
    {
        private readonly IPledgeService _pledge;
        private readonly IAuthorizationService _authz;

        public VerifyController(IPledgeService pledge, IAuthorizationService authz)
        {
            _pledge = pledge;
            _authz = authz;
        }

        // مسار شاشة «ملف الطالب والتحقّق» - مكتوب مرة واحدة.
        private const string FileScreen = "/StudentFile";

        [HttpGet("")]
        public async Task<IActionResult> Index(string? c = null)
        {
            // ⚠️ الصفحة بتُفتح فاضية كمان: اللي بيكتب الرمز بإيده بيدخل على
            //    /Verify من غير معامل، فلازم يلاقي حقل إدخال لا رسالة رفض.
            ViewData["Code"] = c ?? string.Empty;

            // ⚠️ بنسأل نظام الصلاحيات نفسه لا بنقرا الكليم بإيدنا: السياسة هي
            //    التعريف، وأي فحص مكتوب بالإيد بيبقى نسخة تانية منها بتفارقها
            //    أول ما تتغيّر. (نفس قاعدة _Sidebar.cshtml)
            //
            // ⚠️ والتحويل قبل أي قراءة: الموظف اللي معاه ملف الطالب مالوش لازمة
            //    يشوف نسخة مقنّعة من بيانات هو أصلًا بيشوفها كاملة.
            if (!string.IsNullOrWhiteSpace(c)
                && User.Identity?.IsAuthenticated == true
                && (await _authz.AuthorizeAsync(User, ApplicationPermissions.InvestigateStudents.Value)).Succeeded)
            {
                return Redirect(FileScreen + "?q=" + Uri.EscapeDataString(c));
            }

            if (!string.IsNullOrWhiteSpace(c))
            {
                ViewData["Result"] = await _pledge.VerifyDocumentAsync(c);
                ViewData["Checked"] = true;
            }

            return View();
        }
    }
}
