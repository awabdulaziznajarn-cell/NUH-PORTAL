using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  تقسيم الطلاب والطالبات - التعريف الوحيد لقاعدة التصفية.
    //
    //  ⚠️ IUnitOfWork.GetGenderScope موجودة منذ البداية ومكتوب فوقها بالنصّ:
    //     «لو الفلترة اتكتبت بإيد في كل شاشة، أول شاشة تنساها بتبقى ثغرة صامتة».
    //     وقد حدث: الدالة كانت تُستدعى في موضع واحد في النظام كلّه
    //     (RequestService.ScopeToRole)، فبقيت قوائم الطلاب وعدّادات لوحة التحكم
    //     وطوابير سير العمل بلا تقسيم - يرى الموظف أرقام القسم الآخر وقوائمه.
    //
    //  ⚠️ والتصفية على r.StudentGender وحدها لا تكفي: هذا العمود يُملأ في مسار
    //     طلب الموظف فقط، أما طلب التسجيل الذاتي فكان يُنشأ بلا جنس (NULL).
    //     و NULL لا يساوي 'male' ولا 'female' في SQL، فالطلب يختفي عن المشرف
    //     وعن المشرفة معًا ولا يظهر إلا لمن لا قسم له. ولهذا نرجع إلى جنس
    //     الطالب نفسه حين يكون العمود فارغًا - فتعمل السجلات القديمة فورًا
    //     بلا انتظار تحديث البيانات.
    // ============================================================================
    public static class GenderScope
    {
        // طلبات القسم المسموح به. scope == null يعني «الجنسان» فلا تصفية.
        public static IQueryable<Request> ForGender(this IQueryable<Request> query, Gender? scope)
        {
            if (scope == null) return query;
            return query.Where(r =>
                r.StudentGender == scope
                || (r.StudentGender == null && r.Student != null && r.Student.gender == scope));
        }

        // إجراءات الحالة الأكاديمية - تُنسب إلى طالب، فالقسم قسمه.
        public static IQueryable<StudentStatusAction> ForGender(this IQueryable<StudentStatusAction> query, Gender? scope)
        {
            if (scope == null) return query;
            return query.Where(a => a.Student != null && a.Student.gender == scope);
        }

        // عمليات نقل السكن - كذلك.
        // ⚠️ كانت هذه الشرطية مكتوبة بيدها داخل SupervisorHousingTransferService.
        //    نُقلت إلى هنا لا لأنها كانت خطأ - بل لأن كتابتها في كل خدمة هو ما
        //    جعل StudentStatusService تُنسى بلا تقسيم أصلًا.
        public static IQueryable<HousingTransfer> ForGender(this IQueryable<HousingTransfer> query, Gender? scope)
        {
            if (scope == null) return query;
            return query.Where(t => t.Student != null && t.Student.gender == scope);
        }

        // طلاب القسم المسموح به.
        // ⚠️ الطالب بلا جنس مسجَّل لا يظهر لأي قسم - فشلٌ مغلق لا مفتوح. ظهوره
        //    للقسمين تسريبٌ صامت، وظهوره لأحدهما اعتباطًا أسوأ.
        public static IQueryable<Student> ForGender(this IQueryable<Student> query, Gender? scope)
        {
            if (scope == null) return query;
            return query.Where(s => s.gender == scope);
        }

        // ====================================================================
        //  ونفس القاعدة عند *الكتابة* — القسم بيحدّد الجنس، مش الفورم.
        // --------------------------------------------------------------------
        //  ⚠️ التصفية فوق بتخفي سجلات القسم التاني عن الموظف، لكنها ما بتمنعوش
        //     إنه *ينشئ* واحد فيه. مشرف قسم الطلاب كان يقدر يبعت
        //     POST /api/students بـ gender=female فيتخلق سجل طالبة تحت قسم
        //     الطلاب — وبعدها ما يشوفوش هو (خرج عن نطاقه في نفس اللحظة) ولا
        //     تشوفه المشرفة (السجل مالوش وجود في شغلها). سجل يتيم محدش مسؤول
        //     عنه، والطلب المربوط بيه بيقع في نفس الفراغ.
        //     والتعديل زيّه: تغيير جنس طالب قائم بينقله لقسم تاني ويختفي من
        //     قائمة صاحبه من غير ما حد ياخد باله.
        //
        //  ⚠️ والقرار مكتوب هنا جنب قاعدة القراءة عن قصد: لو اتكتب في
        //     StudentService وتاني في BulkRegistrationService، أول واحد يتعدّل
        //     من غير التاني بيفتح نفس الثغرة من الباب التاني.
        // ====================================================================

        // الجنس اللي هيتخزّن فعليًا. اللي نطاقه null (بيشوف القسمين) هو الوحيد
        // اللي اختياره بيتحسب — غيره بياخد قسمه هو مهما بعت في الطلب.
        public static Gender? Resolve(Gender? scope, Gender? requested) => scope ?? requested;

        // ====================================================================
        //  عناوين الشاشة حسب النطاق - «قائمة الطلاب» / «قائمة الطالبات» /
        //  «قائمة الطلاب والطالبات».
        //
        //  ⚠️ ليه لاحقة على مفتاح واحد لا ثلاثة نداءات في كل شاشة: نفس القرار
        //     بيتكرّر في عنوان الصفحة وفي بند القائمة الجانبية وفي ترويسة
        //     الجدول وفي بطاقة الإجمالي - أربع مواضع. أي شرط مكتوب بالإيد في
        //     كل موضع بيخلّي واحد منهم ينسى وتطلع الشاشة بتقول «قائمة الطالبات»
        //     وتحتها «الطلاب المسجلون».
        //
        //  ⚠️ ونطاق الذكور بياخد المفتاح الأصلي بلا لاحقة عن قصد: هو الحالة
        //     اللي كل النصوص القديمة مكتوبة عليها، فما فيش مفتاح قائم بيتغيّر
        //     معناه تحت رجل ترجمة موجودة.
        // ====================================================================
        public static string TitleSuffix(Gender? scope)
            => scope == Gender.Female ? "_f" : scope == null ? "_all" : "";

        public static string TitleKey(string baseKey, Gender? scope)
            => baseKey + TitleSuffix(scope);

        // هل يجوز لصاحب هذا النطاق أن يكتب سجلًا بهذا الجنس؟
        // ⚠️ القيمة الفاضية مسموحة هنا عن قصد: «ما حددش» غير «حدّد القسم
        //    التاني»، وإجبارية الحقل مسؤولية المستدعي لأنها بتختلف من مسار
        //    لمسار (الإكسل بيملّيها من القسم، والفورم بيطلبها).
        public static bool CanWrite(Gender? scope, Gender? value)
            => scope == null || value == null || value == scope;
    }
}
