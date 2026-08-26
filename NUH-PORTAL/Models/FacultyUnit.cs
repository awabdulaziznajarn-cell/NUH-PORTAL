using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Models
{
    // وحدة في سكن أعضاء هيئة التدريس: شقة في برج، أو فيلا.
    //
    // ⚠️ الوحدة هي الثابت، والساكن هو المتغيّر. حساب الدومين (villa08) بيخصّ
    //    الوحدة نفسها — خط الشبكة بتاع الفيلا بيخص الفيلا مش اللي ساكن فيها.
    //    عشان كده الحساب عايش هنا، والسكان في FacultyOccupancy صف لكل واحد.
    //
    // ⚠️ AdAccount بيتقرا من الدومين ومابيتولّدش. المعيار المفروض هو
    //    bu{برج}ap{شقة بخانتين} و villa{رقم بخانتين}، لكن الموجود فعلًا فيه
    //    مخالفات (ba8ap08 بالبادئة الغلط، و villa019 بتلات خانات). لو ولّدنا
    //    الاسم وافترضنا المعيار، الوحدتين دول ما كانش هيتلاقيلهم حساب وكانوا
    //    هيبانوا كأنهم مش موجودين في الدومين. بنسجّل الواقع، وبنعلّم المخالف
    //    في NameMatchesStandard عشان يتصلّح بقرار مننا مش بصمت.
    public class FacultyUnit
    {
        public int Id { get; set; }

        public FacultyUnitType UnitType { get; set; }

        // للأبراج: رقم البرج ورقم الشقة. للفلل: رقم الفيلا. الباقي null.
        // بيتستخرجوا من اسم الحساب وقت الاستيراد، وبيفضلوا null لو الاسم مخالف
        // ومااتقراش — الوحدة بتفضل مسجّلة وشغّالة، بس محتاجة مراجعة.
        public int? TowerNo { get; set; }
        public int? ApartmentNo { get; set; }
        public int? VillaNo { get; set; }

        // اسم الحساب في الدومين زي ما هو بالظبط — مفتاح الربط، ومابيتغيّرش.
        public string AdAccount { get; set; } = string.Empty;

        // الـ DN الكامل — بيتحفظ وقت الاستيراد عشان الكتابة ماتحتاجش بحث كل مرة.
        // ⚠️ بيتحدّث مع كل مزامنة: الوحدات مختلطة، فلما دكتورة تحل محل دكتور
        //    الحساب ممكن يتنقل من OU لـ OU في الدومين ويتغيّر الـ DN. لو
        //    مسكناه ثابت من وقت الاستيراد، أول نقلة هتخلّي الكتابة تفشل بـ
        //    "object not found" ومحدش هيعرف السبب.
        public string? AdDistinguishedName { get; set; }

        // اسم الدخول الكامل (userPrincipalName) زي ما هو في الدليل.
        // ⚠️ بيتقرا ومابيتكتبش: المعرّف اللي النظام بيربط بيه هو AdAccount
        //    (sAMAccountName) - منه بيتقرا رقم البرج والشقة والفيلا، وعليه
        //    بيتم البحث في الدليل. الـ UPN بيتخزّن عشان سؤال واحد: هل مقدّمته
        //    مطابقة لاسم الحساب؟ اختلافهما بيعني إن حساب اتعمل أو اتعدّل من
        //    برّه المعيار، وde بيبان كشارة في سجلّ الوحدة لا كخطأ يوقف إجراء.
        public string? AdUserPrincipalName { get; set; }

        // مقدّمة الـ UPN (قبل @) مطابقة لاسم الحساب؟ محسوبة لا مخزَّنة:
        // قيمة مشتقّة من عمودين، وتخزينها بيفتح باب إنها تتأخّر عنهما.
        public bool UpnMatchesAccount
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AdUserPrincipalName)) return true;
                var at = AdUserPrincipalName.IndexOf('@');
                var local = at > 0 ? AdUserPrincipalName[..at] : AdUserPrincipalName;
                return string.Equals(local, AdAccount, StringComparison.OrdinalIgnoreCase);
            }
        }

        // ⚠️ مافيش Gender على الوحدة عن قصد. تقسيم MALE/FEMALE في الدومين
        //    تنظيمي بس — الأبراج والفلل مختلطة: نفس الشقة ممكن تكون فيها
        //    دكتورة النهاردة ودكتور بعد سنة. لو خزّنّا الجنس على الوحدة كانت
        //    هتفضل متعلّمة «بنات» للأبد بعد أول دكتورة سكنت فيها، وكل تقرير
        //    بعدها يطلع غلط. الجنس بيخصّ الساكن، ومكانه FacultyOccupancy.
        public FacultyUnitStatus Status { get; set; } = FacultyUnitStatus.Active;

        // اسم الحساب مطابق للمعيار؟ false = محتاج مراجعة (زي villa019 و ba8ap08).
        // مش بيمنع أي إجراء، بس بيديك فلتر تشوف المخالفات وتقرر.
        public bool NameMatchesStandard { get; set; } = true;

        // آخر حالة معروفة لحساب الدومين (enabled/disabled) — بتتحدّث مع كل مزامنة
        public bool AdAccountEnabled { get; set; }

        public FacultyUnitSyncState SyncState { get; set; } = FacultyUnitSyncState.Synced;
        public DateTime? LastSyncedAt { get; set; }
        public string? LastSyncError { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        public ICollection<FacultyOccupancy> Occupancies { get; set; } = new List<FacultyOccupancy>();
        public User? CreatedByUser { get; set; }
        public User? UpdatedByUser { get; set; }

        // العرض بالعربي — مصدر واحد للاسم عشان ما يتكتبش في كل شاشة من جديد
        public string DisplayNameAr =>
            UnitType == FacultyUnitType.Villa
                ? (VillaNo.HasValue ? $"فيلا {VillaNo}" : AdAccount)
                : (TowerNo.HasValue && ApartmentNo.HasValue
                    ? $"برج {TowerNo} - شقة {ApartmentNo}"
                    : AdAccount);
    }
}
