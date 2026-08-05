namespace NUH_PORTAL.DTOs.Housing
{
    // وضع المزامنة مع الأكتف دايركتوري.
    // ⚠️ الفصل مقصود: «الربط» بيلمس طلاب لسه مالهمش حساب مسجّل في النظام، وده
    //    اللي فيه خطر لو الرقم الجامعي في الشيت غلط (هيربط الطالب بحساب شخص تاني).
    //    «التحديث» بيلمس المربوطين بالفعل ويقرا حالتهم بس — مالوش أي خطر.
    public enum AdSyncMode
    {
        LinkNew,        // الطلاب اللي مالهمش حساب مربوط — بندوّر ونربط
        RefreshLinked   // الطلاب المربوطين — بنحدّث حالتهم بس، مش بنربط حد جديد
    }

    // نتيجة المزامنة. مفيش إنشاء ولا تعطيل هنا أبدًا: قراءة من الأكتف دايركتوري
    // وكتابة في قاعدة بيانات النظام بس.
    public class AdLinkResultDto
    {
        public string Mode { get; set; } = "";    // linkNew | refreshLinked
        public bool DryRun { get; set; }          // معاينة من غير حفظ

        public int Scanned { get; set; }          // عدد الطلاب اللي اتفحصوا
        public int Linked { get; set; }           // اتربطوا بحساب موجود
        public int AlreadyLinked { get; set; }    // كانوا مربوطين وبيانتهم اتحدّثت
        public int StatusChanged { get; set; }    // حالتهم في الدومين مختلفة عن المسجّلة عندنا
        public int NotFound { get; set; }         // مالهمش حساب في الأكتف دايركتوري
        public int Failed { get; set; }           // فشل الاستعلام (مشكلة اتصال/صلاحية)

        public string? Error { get; set; }        // خطأ عام أوقف العملية

        // أول ٢٠٠ طالب بدون حساب — للعرض في الشاشة من غير ما نغرق الرد
        public List<AdLinkStudentDto> NotFoundStudents { get; set; } = new();

        // اللي حالته اتغيّرت — عشان المشرف يشوف إيه اللي اتعدّل فعلاً
        public List<AdStatusChangeDto> StatusChanges { get; set; } = new();

        // ⚠️ حسابات اسمها في الدومين مالوش أي كلمة مشتركة مع اسم الطالب عندنا.
        //    غالبًا رقم جامعي غلط في الشيت — والربط الغلط خطر لأن «تخرّج» بعد
        //    كده هيعطّل حساب شخص تاني. بنربط وبنبلّغ، والمراجعة على المسؤول.
        public List<AdNameMismatchDto> NameMismatches { get; set; } = new();
    }

    public class AdLinkStudentDto
    {
        public string? StudentId { get; set; }
        public string? FullName { get; set; }
        public string? ExpectedAccount { get; set; }   // الاسم اللي دوّرنا عليه
    }

    public class AdStatusChangeDto
    {
        public string? StudentId { get; set; }
        public string? FullName { get; set; }
        public string? Account { get; set; }
        public string? From { get; set; }              // الحالة المسجّلة عندنا قبل المزامنة
        public string? To { get; set; }                // الحالة الحقيقية في الدومين
    }

    public class AdNameMismatchDto
    {
        public string? StudentId { get; set; }
        public string? SystemName { get; set; }        // الاسم في النظام
        public string? DirectoryName { get; set; }     // الاسم في الأكتف دايركتوري
        public string? Account { get; set; }
    }
}
