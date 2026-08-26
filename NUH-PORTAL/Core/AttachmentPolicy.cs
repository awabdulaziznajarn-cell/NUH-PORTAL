using Microsoft.AspNetCore.Http;
using NUH_PORTAL.Core.Exceptions;

namespace NUH_PORTAL.Core
{
    // ========================================================================
    //  سياسة المرفقات - المصدر الوحيد لما يُقبل رفعه وبأي نوع محتوى يُقدَّم.
    //
    //  ⚠️ لماذا وُجد هذا الملف: قائمة الامتدادات المسموحة كانت مكتوبة مرتين،
    //     في StudentStatusService وفي SupervisorHousingTransferService، وكل
    //     واحدة تتحقق بطريقتها. والنتيجة أن مسارًا منهما كان يشتق نوع المحتوى
    //     من امتداد الملف المخزَّن، والآخر يخزّن file.ContentType القادم من
    //     العميل ويعيد تقديمه كما هو.
    //
    //     والثغرة في الثاني: قائمة الامتدادات لا تحمي، لأن المتصفح يطيع
    //     ترويسة نوع المحتوى لا الامتداد. فيرفع المهاجم ملفًا اسمه x.png
    //     ومحتواه <script> وترويسته text/html، فيُخزَّن النوع كما أعلنه
    //     ويُعاد تقديمه داخل أصل الموقع نفسه - وكل موظف يفتح المرفق يُنفَّذ
    //     السكربت بجلسته هو. و X-Content-Type-Options: nosniff لا يمنع نوعًا
    //     مُعلَنًا صراحةً، فهو يمنع التخمين لا الإعلان.
    //
    //  ثلاث طبقات هنا، كل واحدة تُغلق ما بعدها:
    //     (١) الامتداد من قائمة مغلقة - يمنع .html و .svg و .htm ابتداءً.
    //     (٢) البايتات الأولى تطابق الامتداد - تمنع ملفًا اسمه .png ومحتواه HTML.
    //     (٣) نوع المحتوى يُشتق من الامتداد دائمًا - لا يُقرأ من العميل ولا من
    //         قاعدة البيانات، فالصفوف القديمة المسمومة تُصحَّح عند القراءة.
    // ========================================================================
    public static class AttachmentPolicy
    {
        public const long MaxFileSize = 10 * 1024 * 1024;

        public const string FallbackContentType = "application/octet-stream";

        // الامتداد ← نوع المحتوى الذي نقدّمه به. هذه هي القائمة الوحيدة في النظام.
        private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

        // ⚠️ لا بد أن يبقى بعد AllowedExtensions: مُهيّئات الحقول الساكنة تنفَّذ
        //    بترتيب كتابتها في الملف، فلو سبقها لقرأ قاموسًا لم يُنشأ بعد.
        //    والنص مشتق من القائمة نفسها لا مكتوب بجانبها، فإضافة صيغة جديدة
        //    تظهر في رسالة الرفض تلقائيًا.
        public static string AllowedLabel { get; } =
            string.Join(", ", AllowedExtensions.Keys.Select(e => e.TrimStart('.').ToUpperInvariant()));

        // ⚠️ ما يجوز عرضه داخل الصفحة. غيره يُنزَّل إجباريًا مهما طلب المستخدم،
        //    لأن العرض داخل الصفحة يعني تنفيذه في أصل الموقع.
        //    ملحوظة: docx خارج القائمة عمدًا - لا فائدة من معاينته في المتصفح.
        private static readonly HashSet<string> InlineSafe = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "application/pdf"
        };

        // بصمات البايتات الأولى. الامتداد غير الموجود هنا يمرّ بلا فحص بصمة،
        // لكنه لا يصل أصلًا لأن القائمة أعلاه هي البوابة الأولى.
        private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },                        // %PDF
            [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            // docx حاوية ZIP: الترويسة العادية، والفارغة، والمقسّمة
            [".docx"] = new[]
            {
                new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                new byte[] { 0x50, 0x4B, 0x07, 0x08 }
            }
        };

        // ====================================================================
        //  الرفع: يتحقق من الامتداد والحجم والبصمة، ويعيد نوع المحتوى المشتقّ
        //  الذي يجب تخزينه. لا تخزّن file.ContentType أبدًا.
        // ====================================================================
        public static string ValidateAndResolve(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName);

            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.TryGetValue(ext, out var contentType))
                throw new UserFriendlyException(
                    $"نوع الملف {(string.IsNullOrEmpty(ext) ? "(بلا امتداد)" : ext)} غير مسموح به. الصيغ المسموحة: {AllowedLabel}", 400);

            if (file.Length <= 0)
                throw new UserFriendlyException("الملف فارغ", 400);

            if (file.Length > MaxFileSize)
                throw new UserFriendlyException("حجم الملف يتجاوز 10 ميجابايت", 400);

            if (!MatchesSignature(file, ext))
                throw new UserFriendlyException(
                    "محتوى الملف لا يطابق امتداده. أعد حفظ الملف بصيغته الصحيحة ثم ارفعه.", 400);

            return contentType;
        }

        // ====================================================================
        //  القراءة: نوع المحتوى من امتداد المسار المخزَّن، لا من عمود في
        //  قاعدة البيانات. أي صفّ قديم خُزّن بنوع كاذب يُقدَّم الآن بنوعه الصحيح.
        // ====================================================================
        public static string ResolveContentType(string? pathOrFileName)
        {
            var ext = Path.GetExtension(pathOrFileName ?? "");
            return !string.IsNullOrEmpty(ext) && AllowedExtensions.TryGetValue(ext, out var ct)
                ? ct
                : FallbackContentType;
        }

        public static bool CanRenderInline(string? contentType) =>
            !string.IsNullOrEmpty(contentType) && InlineSafe.Contains(contentType);

        // ====================================================================
        //  الطبقة الرابعة: ترويسة Content-Security-Policy على ردّ الملف نفسه.
        //
        //  ⚠️ الطبقات الثلاث فوق تمنع الملف الخطر من الدخول والخروج. وهذه
        //     تعمل من فوقها: تخبر المتصفح ألّا ينفّذ أي سكربت في هذا الردّ
        //     مهما كان محتواه. فلو نفذ ملف من ثقب لم نتوقعه - عبر صفّ قديم
        //     في قاعدة البيانات، أو مسار رفع يُضاف مستقبلًا وينسى المطوّر
        //     المرور على هذه السياسة - يرفض المتصفح تشغيله، فتتحول المحاولة
        //     من سرقة جلسة الموظف إلى سطر في سجل المتصفح.
        //
        //  ⚠️ ولا يوجد sandbox في السياسة عمدًا: هي أقوى (أصل معزول تمامًا)
        //     لكنها تمنع التنزيل الذي تبدأه الوثيقة نفسها في بعض إصدارات
        //     المتصفحات - ومسار «تنزيل المرفق» عندنا يمرّ من هنا. المنع
        //     المطلوب - تنفيذ السكربتات - يتحقق بـ default-src 'none' وحده.
        //
        //  ⚠️ frame-ancestors 'self' لا 'none': شاشات الموظفين تعرض المعاينة
        //     من أصل الموقع نفسه، وهو نفس ما تسمح به X-Frame-Options: SAMEORIGIN
        //     المضبوطة عالميًا في Program.cs - فلا تتناقض الترويستان.
        //
        //  اختُبرت على متصفح حقيقي: صورة PNG وملف PDF يُعرضان كما هما، وسكربت
        //  مزروع داخل ملف HTML يُرفض قبل التنفيذ.
        // ====================================================================
        public const string ResponseCsp =
            "default-src 'none'; script-src 'none'; object-src 'none'; " +
            "base-uri 'none'; form-action 'none'; frame-ancestors 'self'";

        // ⚠️ تُنادى من كل مسار يقدّم ملفًا رفعه مستخدم. مكانها هنا لا في كل
        //    كنترولر على حدة: نسختان من نفس الترويسة تفترقان مع أول تعديل.
        public static void ApplyResponseHeaders(HttpResponse response)
        {
            response.Headers["Content-Security-Policy"] = ResponseCsp;
            // ⚠️ منع تضمين الملف في صفحة خارجية - مكرّرة هنا عمدًا لأن
            //    frame-ancestors لا تعمل في المتصفحات القديمة، والاثنتان معًا
            //    هما التغطية الكاملة.
            response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            // ⚠️ nosniff مضبوطة عالميًا، لكن ردّ الملف هو أخطر موضع لغيابها،
            //    فتأكيدها هنا يجعل الحماية مستقلة عن ترتيب الـ middleware.
            response.Headers["X-Content-Type-Options"] = "nosniff";
        }

        private static bool MatchesSignature(IFormFile file, string ext)
        {
            if (!Signatures.TryGetValue(ext, out var expected) || expected.Length == 0)
                return true;

            var longest = expected.Max(sig => sig.Length);
            var head = new byte[longest];

            // ⚠️ OpenReadStream تعيد مجرى جديدًا في كل نداء فوق المحتوى نفسه،
            //    فالقراءة هنا لا تستهلك المجرى الذي ينسخ منه التخزين لاحقًا.
            using (var stream = file.OpenReadStream())
            {
                var read = 0;
                while (read < head.Length)
                {
                    var n = stream.Read(head, read, head.Length - read);
                    if (n == 0) break;
                    read += n;
                }

                if (read < expected.Min(sig => sig.Length))
                    return false;

                return expected.Any(sig => sig.Length <= read && head.Take(sig.Length).SequenceEqual(sig));
            }
        }
    }
}
