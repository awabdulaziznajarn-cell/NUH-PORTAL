using Microsoft.AspNetCore.Identity;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Core
{
    // ========================================================================
    //  شكل حساب دخول الطالب — مكان واحد يحدّده، وجهة واحدة تُطبّقه.
    //
    //  ⚠️ العيب الذي عالجه هذا الملف:
    //     الحساب كان يُنشأ مرة واحدة لحظة التحقق برمز الجوال، بسطر واحد:
    //
    //        UserName = "student_" + (student?.student_id ?? mobile)
    //        full_name = student?.full_name ?? "طالب"
    //
    //     والنتيجة أن شكل الحساب يتحدّد بمحض الصدفة: هل كان للطالب سجلّ في
    //     جدول الطلاب في تلك اللحظة أم لا؟
    //
    //       • طالب جديد يتحقق أولًا ثم يقدّم طلبه  →  student_966533333322 واسمه «طالب»
    //       • طالب سجّله المشرف قبل تحققه          →  student_456336111 واسمه الحقيقي
    //
    //     والأسوأ أن الحساب لا يُحدَّث بعدها أبدًا. فالطالب الأول يظل اسمه «طالب»
    //     ورقم جواله في اسم المستخدم إلى الأبد رغم أن النظام صار يعرف رقمه
    //     الجامعي واسمه الكامل بعد دقائق من تقديمه للطلب. جدول واحد بصيغتين،
    //     والبحث والفرز والقراءة تصير أصعب بلا سبب.
    //
    //  القاعدة الآن، وهي واحدة لكل المسارات:
    //     • الرقم الجامعي هو الهوية الثابتة للطالب، فهو أساس اسم المستخدم.
    //     • رقم الجوال هوية مؤقتة تُستعمل قبل ارتباط سجل الطالب فقط — لأنه كل
    //       ما نعرفه لحظة التحقق — وتُرقّى تلقائيًا أول ما يُعرف الرقم الجامعي.
    //     • الاسم الكامل يتبع سجل الطالب دائمًا، و«طالب» نص مؤقت لا أكثر.
    //
    //  الترقية آمنة: الطالب لا يسجّل الدخول باسم المستخدم أصلًا، بل برقم جواله
    //  ورمز التحقق (VerifyAsync تبحث بـ Users.mobile). فاسم المستخدم معرّف
    //  داخلي للعرض والبحث، وتغييره لا يقطع دخول أحد.
    // ========================================================================
    public static class StudentLoginIdentity
    {
        // نص مؤقت يظهر ريثما يرتبط سجل الطالب. ليس اسمًا.
        public const string ProvisionalName = "طالب";

        // ⚠️ عامة عن قصد: شاشة «المستخدمون» بتفرز حسابات دخول الطلاب بالبادئة
        //    دي. لو اتنسخت هناك بقى عندنا تعريفان لحساب الطالب، وأول تعديل
        //    هنا بيخلّي الشاشة تفرز غلط بصمت.
        public const string Prefix = "student_";

        // أرقام فقط — رقم الجوال يصل بصيغ مختلفة (+966… / 966… / مسافات)
        private static string DigitsOnly(string? v) =>
            string.IsNullOrEmpty(v) ? "" : new string(v.Where(char.IsDigit).ToArray());

        // اسم المستخدم المطلوب لهذه الحالة. الرقم الجامعي أولًا، والجوال احتياطًا.
        public static string UserName(string? studentId, string? mobile)
        {
            var id = (studentId ?? "").Trim();
            if (id.Length > 0) return Prefix + id;

            var m = DigitsOnly(mobile);
            return m.Length > 0 ? Prefix + m : Prefix + "unknown";
        }

        // الاسم الكامل المطلوب. سجل الطالب هو المرجع، وإلا النص المؤقت.
        public static string FullName(string? studentFullName)
        {
            var n = (studentFullName ?? "").Trim();
            return n.Length > 0 ? n : ProvisionalName;
        }

        // بريد اصطناعي — لا يُرسل إليه شيء، لكنه مطلوب لهوية ASP.NET Identity.
        // اللاحقة هنا وحدها؛ كانت مكرّرة في OtpFlowService كثابت خاص.
        public const string EmailSuffix = "@std.nuh.edu.sa";
        public static string Email(string? mobile) => DigitsOnly(mobile) + EmailSuffix;

        // الشكل المطلوب لحساب طالب بعينه، من سجله ورقم جواله
        public static (string UserName, string FullName) Desired(Student? student, string? mobile) =>
            (UserName(student?.student_id, mobile), FullName(student?.full_name));

        // مواءمة حساب قائم مع الشكل المطلوب. تُستدعى من كل مسار يعرف الطالب:
        // التحقق برمز الجوال، وتقديم الطلب لحظة إنشاء سجل الطالب. فالترقية تحدث
        // فور معرفة الرقم الجامعي، لا في الدخول التالي.
        //
        // ⚠️ static وتأخذ UserManager بدل أن تكون خدمة مسجَّلة: القاعدة يجب أن
        //    تبقى في ملف واحد، وأي نسخة ثانية منها في خدمة أخرى ستفترق عنها.
        //    UpdateAsync هي التي تعيد حساب NormalizedUserName — لا تُعدّل الحقل
        //    مباشرة على الكيان وإلا صار البحث بالاسم لا يجده.
        public static async Task<bool> SyncAsync(
            UserManager<User> userManager, User user, Student? student, string? mobile)
        {
            var (name, full) = Desired(student, mobile);
            var mail = Email(mobile);
            var changed = false;

            // لا نرقّي إلى الرقم الجامعي إن كان الاسم محجوزًا لحساب آخر — نبقي
            // الاسم المؤقت بدل أن تفشل العملية كلها وتضيع بقية المزامنة.
            if (!string.Equals(user.UserName, name, StringComparison.Ordinal))
            {
                var taken = await userManager.FindByNameAsync(name);
                if (taken == null || taken.Id == user.Id) { user.UserName = name; changed = true; }
            }

            if (!string.Equals(user.full_name, full, StringComparison.Ordinal)
                && !(full == ProvisionalName && !string.IsNullOrWhiteSpace(user.full_name)))
            {
                // لا نستبدل اسمًا حقيقيًا بالنص المؤقت — الاتجاه دائمًا نحو الأدق
                user.full_name = full;
                changed = true;
            }

            if (!string.IsNullOrEmpty(mobile) && !string.Equals(user.mobile, mobile, StringComparison.Ordinal))
            {
                user.mobile = mobile;
                changed = true;
            }

            if (!string.IsNullOrEmpty(mobile) && !string.Equals(user.Email, mail, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = mail;
                changed = true;
            }

            if (changed) await userManager.UpdateAsync(user);
            return changed;
        }
    }
}
