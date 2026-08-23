namespace NUH_PORTAL.Models
{
    // إعدادات سكن أعضاء هيئة التدريس — مسارات الـ OU في الدومين.
    //
    // ⚠️ المسارات في appsettings مش في الكود: نقل OU أو تصليح اسمها في الدومين
    //    لازم يبقى تعديل إعداد وإعادة تشغيل، مش build ونشر.
    //
    // ⚠️ "Buludings" مكتوبة كده بالظبط في الدومين (والصح Buildings). مانصلّحش
    //    الإملاء هنا: الـ DN لازم يطابق الواقع حرفًا بحرف وإلا البحث بيرجع فاضي
    //    والنظام يقول إن مافيش وحدات وهي موجودة. لو اتصلّحت في الدومين يومًا،
    //    تتصلّح هنا في نفس اللحظة.
    //
    // ⚠️ تقسيم MALE/FEMALE تنظيمي في الدومين بس — الوحدات نفسها مختلطة. عشان
    //    كده الاتنين بيتقروا مع بعض في الاستيراد ومابيتخزّنش منهم جنس على
    //    الوحدة. الفايدة الوحيدة من التقسيم إننا نعرف الحساب قاعد فين دلوقتي،
    //    عشان لما ساكن جديد يبقى جنسه مختلف نعرف إن الحساب محتاج ينتقل.
    public class FacultyHousingConfig
    {
        public bool Enabled { get; set; } = true;

        // OU الأبراج — بنين وبنات (تنظيمي)
        public string TowersMaleOu { get; set; } = string.Empty;
        public string TowersFemaleOu { get; set; } = string.Empty;

        // OU الفلل — مختلطة، ممكن تبقى واحدة أو متقسّمة زي الأبراج
        public string VillasOu { get; set; } = string.Empty;
        public string? VillasMaleOu { get; set; }
        public string? VillasFemaleOu { get; set; }

        // ⚠️ سقف عدد النتائج في قراءة الـ OU. الدومين بيرجّع صفحة واحدة افتراضيًا
        //    (١٠٠٠ صف) وبيقطع الباقي بصمت — فلو العدد أكبر لازم paging. السقف
        //    هنا بيخلّي التجاوز يبان كتحذير بدل ما يعدّي ناقص من غير ما حد يعرف.
        public int MaxImportResults { get; set; } = 2000;

        // ====================================================================
        //  الـ OU اللي المفروض الحساب يقعد فيها حسب نوع الوحدة وجنس شاغلها.
        //
        //  ⚠️ بترجّع null لمّا القسم مايكونش متقسّم أصلًا - مش لمّا يكون
        //     الإعداد ناقص بالغلط. الفلل مثلًا ممكن تبقى OU واحدة مختلطة، وفي
        //     الحالة دي مافيش «مكان صح» و«مكان غلط» فمافيش نقل. وnull هنا
        //     معناها «سيبه مكانه» لا «مش عارف» - النداهة بتفرّق.
        //
        //  ⚠️ ولو نوع متقسّم وناحية واحدة بس مضبوطة (بنين موجود وبنات فاضي)،
        //     بترجّع null كمان: النقل لـ OU فاضية بيبوّظ الـ DN ويودّي الحساب
        //     لجذر الدومين. الإعداد الناقص بيتقفل عليه هنا لا بيتنفّذ نصّه.
        // ====================================================================
        public string? TargetOuFor(Enums.FacultyUnitType unitType, Enums.Gender? gender)
        {
            if (gender == null) return null;

            var (male, female) = unitType == Enums.FacultyUnitType.Villa
                ? (VillasMaleOu, VillasFemaleOu)
                : (TowersMaleOu, TowersFemaleOu);

            if (string.IsNullOrWhiteSpace(male) || string.IsNullOrWhiteSpace(female))
                return null;

            return gender == Enums.Gender.Female ? female : male;
        }

        // كل الـ OU اللي بيتقرا منها — مصدر واحد بدل ما كل خدمة تركّب القائمة
        public IEnumerable<string> AllOus()
        {
            if (!string.IsNullOrWhiteSpace(TowersMaleOu)) yield return TowersMaleOu;
            if (!string.IsNullOrWhiteSpace(TowersFemaleOu)) yield return TowersFemaleOu;
            if (!string.IsNullOrWhiteSpace(VillasOu)) yield return VillasOu;
            if (!string.IsNullOrWhiteSpace(VillasMaleOu)) yield return VillasMaleOu!;
            if (!string.IsNullOrWhiteSpace(VillasFemaleOu)) yield return VillasFemaleOu!;
        }
    }
}
