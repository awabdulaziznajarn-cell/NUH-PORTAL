using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // بيحوّل أكواد الكلية/القسم/المبنى/المستوى في الطالب لـ FK ids (dual-write مع الكود القديم).
    // بيخلّي أعمدة الـ FK هي المصدر الأساسي للطلاب الجداد بدون ما نكسر التخزين النصي القديم.
    public interface ILookupResolver
    {
        Task ApplyAsync(Student student);
        Task ApplyAsync(IEnumerable<Student> students);
    }
}
