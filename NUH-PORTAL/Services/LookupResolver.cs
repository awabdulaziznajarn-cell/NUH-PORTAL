using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
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

        // ⚠️ الفحص والرسالة من نفس الجدول: لو المدير ضاف مبنى، بيبان في
        //    المسموح تلقائيًا - ومفيش مكان تاني لازم حد يفتكر يعدّله.
        // ⚠️ والمبنى غير المفعّل بيترفض زي غير الموجود: إيقاف المبنى معناه منع
        //    التسكين فيه، والقايمة المنسدلة بتخفيه أصلًا.
        public async Task<string?> BuildingCodeErrorAsync(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            var all = await _buildings.GetAllAsync();
            var active = all.Where(b => b.IsActive)
                            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Id)
                            .Select(b => b.Code ?? string.Empty)
                            .Where(c => c.Length > 0)
                            .ToList();

            var wanted = Norm(code);
            if (active.Any(c => Norm(c) == wanted)) return null;

            return active.Count == 0
                ? "لا توجد مبانٍ سكنية مفعّلة في النظام - أضفها من شاشة القوائم المرجعية."
                : "رقم المبنى السكني غير صحيح - القيم المسموح بها: " + string.Join("، ", active);
        }

        // أسلوب الترقيم من صفّ المبنى نفسه - مش مشتقّ من الجنس ولا من رقمه.
        // ⚠️ الخريطة بتتحمّل مرة واحدة للـ scope كله: رفع الإكسل بينادي الدالة
        //    دي **لكل صفّ**، فاستعلام لكل صفّ كان هيبقى ٥٠٠ استعلام لشيت واحد.
        private Dictionary<string, HousingNumbering>? _numbering;

        public async Task<HousingNumbering> NumberingForBuildingAsync(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return HousingNumbering.Continuous;

            if (_numbering == null)
            {
                var all = await _buildings.GetAllAsync();
                _numbering = new Dictionary<string, HousingNumbering>();
                foreach (var b in all)
                {
                    var k = Norm(b.Code);
                    if (k.Length > 0) _numbering[k] = b.Numbering;
                }
            }

            return _numbering.TryGetValue(Norm(code), out var n) ? n : HousingNumbering.Continuous;
        }

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
