using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // ========================================================================
    //  تسكين طالب في غرفة - الطريق الوحيد لكتابة بيانات السكن على أي طالب.
    //
    //  ⚠️ قبل كده كل مسار كان بيكتب الخانات بإيده: تسجيل المشرف، تعديل بيانات
    //     طالب، نقل السكن، رفع الإكسل، وبيانات تسجيل الطالب. خمس نسخ لنفس
    //     العملية، وكل واحدة بتتحقّق من حاجة مختلفة - واحدة تفحص الدور، وواحدة
    //     تفحص السعة، وواحدة ماتفحصش أي حاجة. وأول واحدة تُنسى بتفتح الباب كله.
    //
    //  دلوقتي العملية واحدة: تحقّق من البنية (Core/HousingStructure بأسلوب ترقيم
    //  المبنى) → تحقّق من السعة (IHousingCapacityGuard) → كتابة الخمس خانات
    //  وربط المفتاح الأجنبي. اللي عايز يسكّن طالبًا بينادي الدالة دي وبس.
    // ========================================================================
    public interface IHousingPlacement
    {
        //  allowExceptionSlot: المكان الاستثنائي فوق السعة المعتمدة - قرار إداري
        //     بيعمله المشرف، والطالب اللي بيسجّل لنفسه مالوش.
        //  بترمي UserFriendlyException برسالة عربية واضحة لو التسكين مرفوض.
        //  source: من أين جاء التغيير (Core/HousingHistoryKinds.Sources) - يُكتب
        //     في سجل الحركة. وreason وtransferId للنقل: سببه ورقم صفّه.
        Task ApplyAsync(Student student, string? buildingCode, string? floor,
                        string? apartment, string? room, bool allowExceptionSlot,
                        string? source = null, string? reason = null, int? transferId = null);

        // ====================================================================
        //  إخلاء سكن طالب - المسار الوحيد لتفريغ الحقول.
        //
        //  ⚠️ سبب وجودها هنا لا في خدمة الحالة: تفريغ السكن حركة تسكين مثل
        //     التسكين تمامًا، ويجب أن تُسجَّل في سجل الحركة. ولمّا كان التفريغ
        //     مكتوبًا يدويًا في خدمة الحالة كان **ينسى الدور**: يُفرَّغ المبنى
        //     والشقة والغرفة ويبقى رقم الدور في صفّ الطالب بلا معنى.
        // ====================================================================
        Task ClearAsync(Student student, string? reason, string? source);

        // نفس التحقّق بلا كتابة - بيرجّع رسالة الخطأ أو null.
        Task<string?> ValidateAsync(string? buildingCode, string? floor, string? apartment,
                                    string? room, int? excludeStudentId, bool allowExceptionSlot);
    }
}
