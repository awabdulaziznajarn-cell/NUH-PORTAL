using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  توليد رقم الطلب بأمان عند التزاحم — المكان الوحيد لقاعدة إعادة المحاولة.
    //
    //  ⚠️ الرقم بيتولّد بـ «أكبر رقم موجود + ١»، والقراءة دي بتحصل *قبل* الإدخال
    //     بلحظات. فطلبان في نفس الثانية بيقروا نفس الأكبر، وبيولّدوا نفس الرقم،
    //     والفهرس الفريد IX_Requests_request_number بيرفض التاني — فالطالب
    //     التاني بياخد خطأ ٥٠٠ ملوش أي علاقة بيه ولا بطلبه.
    //
    //  ⚠️ ليه إعادة محاولة لا قفل: القفل لازم يفضل ماسك من لحظة القراءة لحد
    //     الإدخال، وده يتطلب معاملة في المسارين — ومسار طلب الموظف مالوش معاملة.
    //     وإعادة المحاولة بتحلّها في المسارين بلا تغيير في بنيتهم، والتصادم نادر
    //     أصلًا فالتكلفة صفر في الحالة العادية.
    //
    //  ⚠️ ومكتوبة هنا مرة واحدة عن قصد: المسارين (تسجيل الطالب لنفسه، وطلب
    //     الموظف) بيولّدوا من نفس التسلسل، ولو كل واحد كتب إعادة محاولته
    //     لافترقوا في عدد المحاولات أو في تمييز الخطأ.
    // ============================================================================
    public static class RequestNumberRetry
    {
        // اسم الفهرس الفريد على عمود رقم الطلب — من RequestConfiguration.
        private const string RequestNumberIndex = "IX_Requests_request_number";

        // 2601 = فهرس فريد، 2627 = قيد فريد. الاتنين معناهم «الرقم اتاخد».
        private static bool IsDuplicateRequestNumber(DbUpdateException ex)
            => ex.InnerException is SqlException sql
               && (sql.Number == 2601 || sql.Number == 2627)
               && sql.Message.Contains(RequestNumberIndex, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// يولّد رقم الطلب ويحفظ. لو الرقم اتاخد في نفس اللحظة، يولّد غيره ويعيد.
        /// </summary>
        /// <param name="generate">يقرأ الأكبر الحالي ويرجّع الرقم التالي.</param>
        /// <param name="assign">يحطّ الرقم على الطلب قبل الحفظ.</param>
        /// <param name="save">الحفظ الفعلي (SaveChanges).</param>
        public static async Task RunAsync(
            Func<Task<string>> generate,
            Action<string> assign,
            Func<Task> save,
            int maxAttempts = 4)
        {
            for (var attempt = 1; ; attempt++)
            {
                assign(await generate());

                try
                {
                    await save();
                    return;
                }
                catch (DbUpdateException ex) when (attempt < maxAttempts && IsDuplicateRequestNumber(ex))
                {
                    // ⚠️ الكيان لسه Added في متتبّع التغييرات، فإعادة التوليد
                    //    والحفظ تاني بتشتغل من غير ما نعيد تركيب الطلب.
                    //    ومفيش رمي هنا عن قصد: التصادم ده تفصيلة داخلية،
                    //    والمستخدم المفروض ياخد رقمه ويكمّل.
                }
            }
        }
    }
}
