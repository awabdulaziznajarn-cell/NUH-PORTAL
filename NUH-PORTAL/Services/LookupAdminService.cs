using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Lookups;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class LookupAdminService : AppServiceBase, ILookupAdminService
    {
        private readonly IRepository<College> _colleges;
        private readonly IRepository<Department> _departments;
        private readonly IRepository<Building> _buildings;
        private readonly IRepository<AcademicLevel> _levels;
        private readonly IRepository<Term> _terms;

        public LookupAdminService(
            IRepository<College> colleges,
            IRepository<Department> departments,
            IRepository<Building> buildings,
            IRepository<AcademicLevel> levels,
            IRepository<Term> terms,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _colleges = colleges;
            _departments = departments;
            _buildings = buildings;
            _levels = levels;
            _terms = terms;
        }

        private static string Norm(string? c) => (c ?? "").Trim().ToLowerInvariant();

        private static LookupDto Map(College c) => new() { Id = c.Id, Code = c.Code, ArName = c.ArName, EnName = c.EnName, DisplayOrder = c.DisplayOrder, IsActive = c.IsActive };
        private static LookupDto Map(AcademicLevel a) => new() { Id = a.Id, Code = a.Code, ArName = a.ArName, EnName = a.EnName, DisplayOrder = a.DisplayOrder, IsActive = a.IsActive };
        private static LookupDto Map(Building b) => new() { Id = b.Id, Code = b.Code, ArName = b.ArName, EnName = b.EnName, DisplayOrder = b.DisplayOrder, IsActive = b.IsActive, Gender = b.Gender };
        private static LookupDto Map(Department d) => new() { Id = d.Id, Code = d.Code, ArName = d.ArName, EnName = d.EnName, DisplayOrder = d.DisplayOrder, IsActive = d.IsActive, CollegeId = d.CollegeId, CollegeName = d.College != null ? d.College.ArName : null };

        public async Task<List<LookupDto>> ListAsync(string category)
        {
            switch (Norm(category))
            {
                case "college":
                    return (await _colleges.GetAllAsync()).OrderBy(x => x.DisplayOrder).Select(Map).ToList();
                case "building":
                    return (await _buildings.GetAllAsync()).OrderBy(x => x.DisplayOrder).Select(Map).ToList();
                case "academiclevel":
                    return (await _levels.GetAllAsync()).OrderBy(x => x.DisplayOrder).Select(Map).ToList();
                case "department":
                    var deps = await _departments.FindAllAsync(null, d => d.College!);
                    return deps.OrderBy(x => x.DisplayOrder).Select(Map).ToList();
                default:
                    throw new UserFriendlyException("نوع قائمة غير معروف", 400);
            }
        }

        public async Task<LookupDto> SaveAsync(string category, int? id, LookupSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.ArName) || string.IsNullOrWhiteSpace(dto.EnName))
                throw new UserFriendlyException("الكود والاسم العربي والإنجليزي مطلوبين", 400);

            switch (Norm(category))
            {
                case "college":
                {
                    var e = id.HasValue ? await _colleges.GetByIdAsync(id.Value) ?? throw UserFriendlyException.NotFound("غير موجود") : new College();
                    e.Code = dto.Code; e.ArName = dto.ArName; e.EnName = dto.EnName; e.DisplayOrder = dto.DisplayOrder; e.IsActive = dto.IsActive;
                    if (!id.HasValue) await _colleges.AddAsync(e); else _colleges.Update(e);
                    await UnitOfWork.SaveAsync();
                    return Map(e);
                }
                case "building":
                {
                    var e = id.HasValue ? await _buildings.GetByIdAsync(id.Value) ?? throw UserFriendlyException.NotFound("غير موجود") : new Building();
                    e.Code = dto.Code; e.ArName = dto.ArName; e.EnName = dto.EnName; e.DisplayOrder = dto.DisplayOrder; e.IsActive = dto.IsActive; e.Gender = dto.Gender;
                    if (!id.HasValue) await _buildings.AddAsync(e); else _buildings.Update(e);
                    await UnitOfWork.SaveAsync();
                    return Map(e);
                }
                case "academiclevel":
                {
                    var e = id.HasValue ? await _levels.GetByIdAsync(id.Value) ?? throw UserFriendlyException.NotFound("غير موجود") : new AcademicLevel();
                    e.Code = dto.Code; e.ArName = dto.ArName; e.EnName = dto.EnName; e.DisplayOrder = dto.DisplayOrder; e.IsActive = dto.IsActive;
                    if (!id.HasValue) await _levels.AddAsync(e); else _levels.Update(e);
                    await UnitOfWork.SaveAsync();
                    return Map(e);
                }
                case "department":
                {
                    var e = id.HasValue ? await _departments.GetByIdAsync(id.Value) ?? throw UserFriendlyException.NotFound("غير موجود") : new Department();
                    e.Code = dto.Code; e.ArName = dto.ArName; e.EnName = dto.EnName; e.DisplayOrder = dto.DisplayOrder; e.IsActive = dto.IsActive; e.CollegeId = dto.CollegeId;
                    if (!id.HasValue) await _departments.AddAsync(e); else _departments.Update(e);
                    await UnitOfWork.SaveAsync();
                    return Map(e);
                }
                default:
                    throw new UserFriendlyException("نوع قائمة غير معروف", 400);
            }
        }

        public async Task DeleteAsync(string category, int id)
        {
            try
            {
                switch (Norm(category))
                {
                    case "college": _colleges.Remove(await _colleges.GetByIdAsync(id) ?? throw UserFriendlyException.NotFound("غير موجود")); break;
                    case "building": _buildings.Remove(await _buildings.GetByIdAsync(id) ?? throw UserFriendlyException.NotFound("غير موجود")); break;
                    case "academiclevel": _levels.Remove(await _levels.GetByIdAsync(id) ?? throw UserFriendlyException.NotFound("غير موجود")); break;
                    case "department": _departments.Remove(await _departments.GetByIdAsync(id) ?? throw UserFriendlyException.NotFound("غير موجود")); break;
                    default: throw new UserFriendlyException("نوع قائمة غير معروف", 400);
                }
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException)
            {
                throw new UserFriendlyException("لا يمكن الحذف - العنصر مستخدم من قِبل طلاب. عطّله بدل الحذف.", 409);
            }
        }

        public async Task<List<TermDto>> ListTermsAsync()
        {
            var items = await _terms.GetAllAsync();
            return items.OrderBy(t => t.DisplayOrder)
                .Select(t => new TermDto { Id = t.Id, ArText = t.ArText, EnText = t.EnText, DisplayOrder = t.DisplayOrder, IsActive = t.IsActive })
                .ToList();
        }

        public async Task<TermDto> SaveTermAsync(int? id, TermSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ArText) || string.IsNullOrWhiteSpace(dto.EnText))
                throw new UserFriendlyException("النص العربي والإنجليزي مطلوبين", 400);

            var e = id.HasValue ? await _terms.GetByIdAsync(id.Value) ?? throw UserFriendlyException.NotFound("غير موجود") : new Term();
            e.ArText = dto.ArText; e.EnText = dto.EnText; e.DisplayOrder = dto.DisplayOrder; e.IsActive = dto.IsActive;
            if (!id.HasValue) await _terms.AddAsync(e); else _terms.Update(e);
            await UnitOfWork.SaveAsync();
            return new TermDto { Id = e.Id, ArText = e.ArText, EnText = e.EnText, DisplayOrder = e.DisplayOrder, IsActive = e.IsActive };
        }

        public async Task DeleteTermAsync(int id)
        {
            _terms.Remove(await _terms.GetByIdAsync(id) ?? throw UserFriendlyException.NotFound("غير موجود"));
            await UnitOfWork.SaveAsync();
        }
    }
}
