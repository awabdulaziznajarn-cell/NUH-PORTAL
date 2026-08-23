namespace NUH_PORTAL.DTOs.Registration
{
    public class StartRegistrationRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public object? RegistrationData { get; set; }

        // ⚠️ التعهّد بيتبعت **مع** الطلب لا في نداء تاني بعده.
        //    الترتيب في الواجهة: الجوال ← الإقرار ← النموذج ← التقديم. يعني
        //    وقت شاشة الإقرار الطلب لسه ماتخلقش أصلًا ومفيش requestId يتبعت
        //    عليه — ودي كانت السبب الحقيقي إن الشاشة كانت بتكتب في تخزين
        //    المتصفح وتعدّي: مكانش قدّامها API تناديه في اللحظة دي.
        //
        //    ولو خلّينا الشاشة تنادي بعد التقديم كان ممكن الطلب ينجح والنداء
        //    التاني يفشل (شبكة، إقفال تبويب) — فيبقى في الجدول طلب من غير
        //    تعهّد، وده بالظبط اللي بنأمّن ضده. لمّا الاتنين في نفس النداء
        //    بيبقوا في نفس المعاملة: يا الاتنين يا ولا واحد.
        public AcceptDeclarationsRequest? Pledge { get; set; }
    }

    // ⚠️ نفس الشكل ده بيوصل في الحالتين: مع /start (المسار الطبيعي) وعلى
    //    /{requestId}/declarations. شكل واحد للتعهّد في النظام كله.
    public class AcceptDeclarationsRequest
    {
        public bool DeclarationAccepted { get; set; }
        public bool PolicyAccepted { get; set; }

        // ⚠️ PolicyVersion اتشال من هنا: كان بيوصل من العميل كنص "1.0" ثابت،
        //    والخادم بياخده زي ما هو. دلوقتي بيتحسب على الخادم من بصمة البنود
        //    (Core/PledgeRules.VersionOf) — والعميل ما بقاش له رأي فيه.

        // الجملة اللي الطالب كتبها. الخادم بيطبّعها ويقارنها، ويرفض لو مختلفة.
        public string? TypedConfirmation { get; set; }

        // بصمة البنود اللي الواجهة كانت عارضاها وقت الكتابة.
        // ⚠️ مش مصدر ثقة — الخادم بيبني بصمته بنفسه. دي بتتقارن بيها عشان
        //    نعرف إن المدير عدّل البنود بين لحظة القراية ولحظة الإرسال، فنقول
        //    للطالب «البنود اتحدّثت، اقرأها من جديد» بدل ما نسجّل موافقته على
        //    نصّ هو أصلًا ماشافهوش.
        public string? TermsHash { get; set; }
    }

    // ====================================================================
    //  وثيقة التعهّد كما يقدّمها الخادم — النصّ والبصمة مع بعض.
    //
    //  ⚠️ العربي والإنجليزي في نفس الرد عن قصد: العرض بيتغيّر بلغة الواجهة،
    //     لكن البصمة على العربي دايمًا (الشرح في Core/PledgeRules.cs).
    //     لو الصفحة كانت بتجيب البنود من /api/lookups/terms — واللي بيرجّع
    //     لغة واحدة حسب الطلب — كانت هتحسب بصمة على نصّ مختلف لكل لغة.
    // ====================================================================
    public class PledgeTermItemDto
    {
        public int Order { get; set; }
        public string Ar { get; set; } = string.Empty;
        public string En { get; set; } = string.Empty;
    }

    public class PledgeDocumentDto
    {
        public List<PledgeTermItemDto> Items { get; set; } = new();

        // النصّ العربي المجمَّد بالحرف — هو اللي البصمة اتحسبت عليه.
        public string Text { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;

        // الجملة المطلوب كتابتها — من Core/PledgeRules، عشان الصفحة تعرضها
        // من غير ما تكتبها بنفسها نسخة تانية.
        public string RequiredSentence { get; set; } = string.Empty;
    }

    // ====================================================================
    //  تعهّد طلب بعينه كما هو محفوظ — لشاشة تفاصيل الطلب.
    //
    //  ⚠️ البنود هنا هي **المحفوظة** لا الحالية: النصّ اللي الطالب شافه
    //     ووافق عليه، حتى لو المدير عدّل البنود بعده بعشر مرات.
    // ====================================================================
    public class PledgeRecordDto
    {
        public DateTime AcceptedAt { get; set; }
        public string? PolicyVersion { get; set; }
        public string? TermsHash { get; set; }
        public string? TypedConfirmation { get; set; }
        public List<string> Terms { get; set; } = new();
        public string? IpAddress { get; set; }

        // البنود المعمول بها الآن ≠ البنود وقت التوقيع.
        // ⚠️ الشاشة بتلوّن الشريط على أساسها، والمراجع بيعرف إن اللي في شاشة
        //    القوائم مش هو المُلزِم لهذا الطلب.
        public bool TermsChanged { get; set; }

        // تعهّد قديم بلا نصّ محفوظ (اتكتب قبل تفعيل التوثيق).
        public bool Documented { get; set; }
    }

    // فحص مبكر للتكرار من داخل النموذج.
    // ⚠️ الحقلان مطلوبان معًا عن قصد: فحص رقم الهوية وحده يحوّل المسار إلى أداة
    //    استعلام — يكتب المهاجم أرقام هوية بالتتابع فيعرف مَن المسجَّل في الإسكان.
    //    باشتراط تطابق الرقم الجامعي ورقم الهوية لنفس السجل، مَن يملك الاثنين
    //    صحيحين يعرف صاحبهما أصلًا فلا يكتسب معلومة جديدة.
    //    (نفس قاعدة شاشة التتبع: رقم الطلب وحده لا يكفي، ومعه آخر ٤ أرقام.)
    public class DuplicateCheckRequest
    {
        public string? StudentId { get; set; }
        public string? NationalId { get; set; }
    }

    public class DuplicateCheckResultDto
    {
        public bool Found { get; set; }
        public string? Message { get; set; }

        // ⚠️ لا يوجد RequestNumber هنا عن قصد. هذا الفحص لا يثبت ملكية الطلب
        //    (يطابق على الرقم الجامعي والهوية دون الجوال)، وما لا يُرسَل لا يُسرَّب.
    }

    public class ResubmitRequest
    {
        public string? RegistrationData { get; set; }

        // ⚠️ مخرج الطالب لو المراجع علّم على خانة غلط. من غيره الطالب بيبقى
        //    مقفول على خانات مش هي المشكلة ومفيش طريقة يوصّل بيها ده.
        public string? StudentNote { get; set; }
    }

    public class StartRegistrationResultDto
    {
        public string? Message { get; set; }
        public int RequestId { get; set; }
        public string? RequestNumber { get; set; }
    }

    public class MyRequestListItemDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? StudentName { get; set; }
        public string? StudentId { get; set; }
        public string? StudentPhone { get; set; }
    }

    public class MyRequestStudentDto
    {
        public string? student_id { get; set; }
        public string? full_name { get; set; }
        public string? national_id { get; set; }
        public string? college { get; set; }
        public string? department { get; set; }
        public string? phone { get; set; }
        public string? ad_username { get; set; }
    }

    public class MyRequestDetailDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public string? RegistrationData { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? Notes { get; set; }

        // الخانات اللي المراجع طلب تصحيحها — الباقي بيتقفل في نموذج الطالب.
        // فاضية = كل الخانات مفتوحة.
        public List<string> InfoFields { get; set; } = new();
        public MyRequestStudentDto? Student { get; set; }
        public List<NUH_PORTAL.DTOs.Workflow.WorkflowHistoryItemDto> History { get; set; } = new();
    }
}
