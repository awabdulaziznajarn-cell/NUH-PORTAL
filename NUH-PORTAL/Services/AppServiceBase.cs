using MapsterMapper;
using NUH_PORTAL.Core;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services
{
    // الأساس لكل الـ services: بيوفّر UnitOfWork + Mapper بالوراثة (زي permits)
    public abstract class AppServiceBase
    {
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly IMapper Mapper;

        protected AppServiceBase(IUnitOfWork unitOfWork, IMapper mapper)
        {
            UnitOfWork = unitOfWork;
            Mapper = mapper;
        }

        // ====================================================================
        //  تقسيم الطلاب والطالبات — البوابة الوحيدة لأي استعلام
        // --------------------------------------------------------------------
        //  ⚠️ القاعدة نفسها في Core/GenderScope.cs، والمصدر في
        //     IUnitOfWork.GetGenderScope. المشكلة ماكانتش في القاعدة — كانت في
        //     إن كل خدمة بتفتكر تستدعيها. والنتيجة: القوائم كانت مقسّمة
        //     والجلب بالمعرّف لأ. مشرف قسم يفتح
        //     GET /api/Students/by-number/<رقم من القسم التاني> فيقرا السجل
        //     كامل ويعدّله — الشاشة ماكانتش بتعرضه له، والـ API كان بيعرضه.
        //
        //  ⚠️ الحلّ مش إضافة ForGender في كل دالة — ده نفس الاعتماد على
        //     التذكّر اللي وقّعنا. الحلّ إن الوصول للجدول يمرّ من هنا:
        //
        //         Scoped(_students.Query()).FirstOrDefaultAsync(s => s.Id == id)
        //
        //     فالنطاق بيتحدد في مكان واحد، وأي ‎_students.Query()‎ عارية في
        //     خدمة بتبقى علامة ظاهرة في المراجعة لا سهوًا صامتًا.
        //
        //  ⚠️ الأحمال الزائدة (overloads) بالنوع عن قصد: المستدعي ما بيمرّرش
        //     النطاق أصلًا، فما ينفعش يمرّر النطاق الغلط.
        // ====================================================================
        protected IQueryable<Student> Scoped(IQueryable<Student> query)
            => query.ForGender(UnitOfWork.GetGenderScope());

        protected IQueryable<Request> Scoped(IQueryable<Request> query)
            => query.ForGender(UnitOfWork.GetGenderScope());

        protected IQueryable<StudentStatusAction> Scoped(IQueryable<StudentStatusAction> query)
            => query.ForGender(UnitOfWork.GetGenderScope());

        protected IQueryable<HousingTransfer> Scoped(IQueryable<HousingTransfer> query)
            => query.ForGender(UnitOfWork.GetGenderScope());
    }
}
