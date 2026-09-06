using System.Globalization;
using NUH_PORTAL.DTOs.Lookups;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // خدمة القوائم المرجعية — تعتمد على IRepository العام لكل كيان (متسجّل تلقائيًا)
    public class LookupService : ILookupService
    {
        private readonly IRepository<College> _colleges;
        private readonly IRepository<Department> _departments;
        private readonly IRepository<Building> _buildings;
        private readonly IRepository<AcademicLevel> _levels;
        private readonly IRepository<Term> _terms;

        public LookupService(
            IRepository<College> colleges,
            IRepository<Department> departments,
            IRepository<Building> buildings,
            IRepository<AcademicLevel> levels,
            IRepository<Term> terms)
        {
            _colleges = colleges;
            _departments = departments;
            _buildings = buildings;
            _levels = levels;
            _terms = terms;
        }

        private static bool IsAr => CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ar";

        public async Task<List<LookupItemDto>> GetCollegesAsync()
        {
            var items = await _colleges.FindAllAsync(c => c.IsActive);
            return items.OrderBy(c => c.DisplayOrder)
                .Select(c => new LookupItemDto { Id = c.Id, Code = c.Code, Name = IsAr ? c.ArName : c.EnName })
                .ToList();
        }

        public async Task<List<LookupItemDto>> GetDepartmentsAsync(int? collegeId)
        {
            var items = await _departments.FindAllAsync(d => d.IsActive && (collegeId == null || d.CollegeId == collegeId));
            return items.OrderBy(d => d.DisplayOrder)
                .Select(d => new LookupItemDto { Id = d.Id, Code = d.Code, Name = IsAr ? d.ArName : d.EnName })
                .ToList();
        }

        public async Task<List<BuildingItemDto>> GetBuildingsAsync(Gender? gender)
        {
            var items = await _buildings.FindAllAsync(b => b.IsActive && (gender == null || b.Gender == gender));
            return items.OrderBy(b => b.DisplayOrder)
                .Select(b => new BuildingItemDto
                {
                    Id = b.Id,
                    Code = b.Code,
                    Name = IsAr ? b.ArName : b.EnName,
                    // أسلوب الترقيم والسعة بيمشوا مع المبنى: الشاشة بتبني قوائم
                    // الشقق والغرف منهم، فمبنى جديد بيشتغل من غير أي تعديل كود.
                    Numbering = b.Numbering.ToString(),
                    RoomCapacity = b.RoomCapacity,
                    RoomCapacityMax = b.RoomCapacityMax
                })
                .ToList();
        }

        public async Task<List<LookupItemDto>> GetAcademicLevelsAsync()
        {
            var items = await _levels.FindAllAsync(a => a.IsActive);
            return items.OrderBy(a => a.DisplayOrder)
                .Select(a => new LookupItemDto { Id = a.Id, Code = a.Code, Name = IsAr ? a.ArName : a.EnName })
                .ToList();
        }

        public async Task<List<TermItemDto>> GetTermsAsync()
        {
            var items = await _terms.FindAllAsync(t => t.IsActive);
            return items.OrderBy(t => t.DisplayOrder)
                .Select(t => new TermItemDto { Id = t.Id, Text = IsAr ? t.ArText : t.EnText, DisplayOrder = t.DisplayOrder })
                .ToList();
        }
    }
}
