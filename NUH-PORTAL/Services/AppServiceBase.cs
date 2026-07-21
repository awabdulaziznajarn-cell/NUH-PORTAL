using MapsterMapper;
using NUH_PORTAL.Data.Interfaces;

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
    }
}
