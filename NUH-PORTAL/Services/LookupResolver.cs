using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // بيبني خرائط code→id مرة واحدة لكل scope ويحلّها في الميموري — مناسب للتسجيل الفردي والجملة.
    // لو الكود مش متطابق مع أي عنصر، الـ FK بيتسِب null (أمين: منخزّنش ربط غلط).
    public class LookupResolver : ILookupResolver
    {
        private readonly IRepository<College> _colleges;
        private readonly IRepository<Department> _departments;
        private readonly IRepository<Building> _buildings;
        private readonly IRepository<AcademicLevel> _levels;

        private Dictionary<string, int>? _col, _dep, _bld, _lvl;
        private bool _loaded;

        public LookupResolver(
            IRepository<College> colleges,
            IRepository<Department> departments,
            IRepository<Building> buildings,
            IRepository<AcademicLevel> levels)
        {
            _colleges = colleges;
            _departments = departments;
            _buildings = buildings;
            _levels = levels;
        }

        private static string Norm(string? c) => (c ?? "").Trim().ToLowerInvariant();

        private static Dictionary<string, int> BuildMap<T>(IEnumerable<T> items, Func<T, string?> code, Func<T, int> id)
        {
            var d = new Dictionary<string, int>();
            foreach (var it in items)
            {
                var k = Norm(code(it));
                if (k.Length > 0 && !d.ContainsKey(k)) d[k] = id(it);
            }
            return d;
        }

        private async Task EnsureLoadedAsync()
        {
            if (_loaded) return;
            _col = BuildMap(await _colleges.GetAllAsync(), x => x.Code, x => x.Id);
            _dep = BuildMap(await _departments.GetAllAsync(), x => x.Code, x => x.Id);
            _bld = BuildMap(await _buildings.GetAllAsync(), x => x.Code, x => x.Id);
            _lvl = BuildMap(await _levels.GetAllAsync(), x => x.Code, x => x.Id);
            _loaded = true;
        }

        private static int? Resolve(Dictionary<string, int>? map, string? code)
        {
            if (map == null) return null;
            var k = Norm(code);
            return (k.Length > 0 && map.TryGetValue(k, out var id)) ? id : (int?)null;
        }

        public async Task ApplyAsync(Student student)
        {
            await EnsureLoadedAsync();
            ApplyCore(student);
        }

        public async Task ApplyAsync(IEnumerable<Student> students)
        {
            await EnsureLoadedAsync();
            foreach (var s in students) ApplyCore(s);
        }

        private void ApplyCore(Student s)
        {
            s.CollegeId = Resolve(_col, s.college);
            s.DepartmentId = Resolve(_dep, s.department);
            s.BuildingId = Resolve(_bld, s.housing_building);
            s.AcademicLevelId = Resolve(_lvl, s.academic_level);
        }
    }
}
