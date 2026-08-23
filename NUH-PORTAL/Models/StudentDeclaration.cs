namespace NUH_PORTAL.Models
{
    public class StudentDeclaration
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public bool DeclarationAccepted { get; set; }
        public bool PolicyAccepted { get; set; }

        // ⚠️ بقى مشتقًّا من بصمة البنود (Core/PledgeRules.VersionOf) لا مكتوبًا
        //    من العميل. كان بيوصل من الواجهة كنص "1.0" ثابت على كل السجلات.
        public string PolicyVersion { get; set; } = string.Empty;

        // ====================================================================
        //  توثيق التعهّد — البنود المجمَّدة وبصمتها والجملة المكتوبة.
        //
        //  ⚠️ التلاتة nullable عن قصد: أي سجل اتكتب قبل الميجريشن دي مالوش
        //     نصّ ولا بصمة، و null بتقول كده بوضوح. لو كانت "" كنّا هنبصّ على
        //     سجل قديم فنفتكره تعهّدًا على بنود فاضية.
        //     أي سجل جديد بيتكتب بالتلاتة مملوءة — الخادم بيرفض غير كده.
        // ====================================================================

        // نصّ البنود كما كان لحظة الموافقة — مبني على الخادم من جدول Terms.
        // ⚠️ ده مش تكرار للجدول: الجدول بيتغيّر، وده الشاهد على لحظة بعينها.
        public string? TermsText { get; set; }

        // SHA-256 للنصّ اللي فوق (hex). أرخص وأدقّ من مقارنة نصّين طويلين لما
        // نحب نعرف: التعهّدين دول على نفس البنود ولا لأ؟
        public string? TermsHash { get; set; }

        // الجملة اللي الطالب كتبها بخطّ إيده — تتخزّن **كما كتبها** لا بعد
        // التطبيع. التطبيع للمقارنة بس؛ اللي يتحفظ للسجل هو فعله هو.
        public string? TypedConfirmation { get; set; }

        public DateTime AcceptedDate { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }

        public Request? Request { get; set; }
    }
}
