using Microsoft.EntityFrameworkCore.Storage;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Interfaces
{
    // وحدة العمل: الحفظ + هوية/دور المستخدم الحالي (من الـ JWT) + المعاملات
    public interface IUnitOfWork
    {
        Task<bool> SaveAsync();
        int GetCurrentUserId();
        string? GetCurrentUserRole();
        // فحص صلاحية المستخدم الحالي جوّه الـ service. الفحص على مستوى الكنترولر
        // بيقفل الـ endpoint كله، لكن في حالات القرار بيعتمد على مرحلة الطلب —
        // زي اعتماد طلب: مين يعتمد بيتحدّد من مرحلته مش من مين طالب الاعتماد.
        bool HasPermission(string permission);

        // ⚠️ قاعدة واحدة لتقسيم الطلاب/الطالبات، وكل الشاشات بتقرا منها هي وبس.
        //    بترجّع null يعني «يشوف الجنسين» — إما لأن دوره فيه صلاحية
        //    students.allGenders، أو لأن حسابه مش محدَّد له قسم أصلًا.
        //    وبترجّع Male أو Female يعني «القسم ده وبس».
        //    لو الفلترة اتكتبت بإيد في كل شاشة، أول شاشة تنساها بتبقى ثغرة
        //    صامتة: المشرفة تشوف بيانات مش من قسمها ومحدش ياخد باله.
        Gender? GetGenderScope();
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
