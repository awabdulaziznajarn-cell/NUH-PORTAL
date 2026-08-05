using Microsoft.AspNetCore.Http;

namespace NUH_PORTAL.Services.Interfaces
{
    // مكان تخزين مرفقات المستخدمين على الديسك.
    //
    // قبل كده كانت المرفقات بتتكتب جوه wwwroot/uploads — يعني جوه مجلد النشر
    // نفسه. ده كان بيخلّي أي نشر بينضّف المجلد يمسح مستندات الطلاب معاه، وكمان
    // بيخلّي الملفات في متناول UseStaticFiles من غير أي تحقق صلاحية.
    //
    // دلوقتي التخزين برّه المشروع تمامًا (Storage:AttachmentsRoot)، ومنظّم
    // بالطالب — انظر AttachmentStorage للشكل الكامل.
    public interface IAttachmentStorage
    {
        // المجلد الجذر — مثال: D:\NUH-DATA
        string Root { get; }

        // بيحفظ الملف ويرجّع **المسار النسبي للجذر** عشان يتخزّن في الداتابيز.
        // تخزين المسار كامل (مش اسم الملف بس) بيخلّي أي إعادة تنظيم مستقبلية
        // ماتكسرش الصفوف القديمة — النظام بيقرا المسار من الصف بدل ما يفترض شكل المجلدات.
        Task<string> SaveAsync(
            string category,
            string universityId,
            string recordKind,
            int recordId,
            string? uploadedBy,
            IFormFile file);

        // بيحوّل القيمة المخزّنة لمسار فعلي على الديسك، وبيدعم الشكل القديم
        // (اسم ملف بس + مجلد باسم رقم السجل). بيرجّع null لو الملف مش موجود.
        string? ResolveExisting(string category, int recordId, string? storedPath);
    }
}
