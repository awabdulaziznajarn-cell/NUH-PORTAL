using MapsterMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;
using System.Text.RegularExpressions;

namespace NUH_PORTAL.Services
{
    // كل منطق الطلاب اتنقل هنا من الكنترولر (Controller بقى رفيع بيوجّه بس)
    public class StudentService : AppServiceBase, IStudentService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Request> _requests;
        private readonly IRepository<AccountLifecycleLog> _lifecycle;
        private readonly IAuditService _audit;
        private readonly ILookupResolver _lookups;

        private static readonly HashSet<string> ValidBuildings =
            new() { "40", "41", "42", "43", "65", "66", "67", "68", "69", "70" };

        public StudentService(
            IRepository<Student> students,
            IRepository<Request> requests,
            IRepository<AccountLifecycleLog> lifecycle,
            IAuditService audit,
            ILookupResolver lookups,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _requests = requests;
            _lifecycle = lifecycle;
            _audit = audit;
            _lookups = lookups;
        }

        public async Task<List<StudentDto>> GetStudentsAsync(bool showDeleted, string? adStatus)
        {
            var list = await BuildStudentsQuery(showDeleted, adStatus).ToListAsync();
            return Mapper.Map<List<StudentDto>>(list);
        }

        public async Task<QueryResult<StudentDto>> GetPagedAsync(QueryParams queryParams, bool showDeleted, string? adStatus)
        {
            var query = BuildStudentsQuery(showDeleted, adStatus);

            // بحث حر في أهم الحقول
            var f = queryParams.FilterText?.Trim();
            if (!string.IsNullOrEmpty(f))
            {
                query = query.Where(s =>
                    (s.student_id != null && s.student_id.Contains(f)) ||
                    (s.full_name != null && s.full_name.Contains(f)) ||
                    (s.full_name_english != null && s.full_name_english.Contains(f)) ||
                    (s.national_id != null && s.national_id.Contains(f)) ||
                    (s.phone != null && s.phone.Contains(f)) ||
                    (s.college != null && s.college.Contains(f)));
            }

            // ترتيب
            query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortAsc) switch
            {
                ("student_id", true) => query.OrderBy(s => s.student_id),
                ("student_id", false) => query.OrderByDescending(s => s.student_id),
                ("full_name", true) => query.OrderBy(s => s.full_name),
                ("full_name", false) => query.OrderByDescending(s => s.full_name),
                ("college", true) => query.OrderBy(s => s.college),
                ("college", false) => query.OrderByDescending(s => s.college),
                ("created_at", true) => query.OrderBy(s => s.created_at),
                ("created_at", false) => query.OrderByDescending(s => s.created_at),
                ("status", true) => query.OrderBy(s => s.status),
                ("status", false) => query.OrderByDescending(s => s.status),
                ("id", true) => query.OrderBy(s => s.Id),
                _ => query.OrderByDescending(s => s.Id)
            };

            var result = await query.ToPagedResultAsync(queryParams);
            return result.Map<Student, StudentDto>(Mapper);
        }

        private IQueryable<Student> BuildStudentsQuery(bool showDeleted, string? adStatus)
        {
            var query = _students.Query().AsNoTracking();
            if (!showDeleted)
                query = query.Where(s => !s.IsDeleted);

            if (!string.IsNullOrEmpty(adStatus))
            {
                query = adStatus.ToLower() switch
                {
                    "enabled" => query.Where(s => s.ad_status == AdStatus.enabled),
                    "disabled" => query.Where(s => s.ad_status == AdStatus.disabled),
                    "none" => query.Where(s => s.ad_status == null),
                    "any" => query.Where(s => s.ad_status != null),
                    _ => query
                };
            }

            return query;
        }

        public async Task<StudentStatsDto> GetStatsAsync()
        {
            // عدّادات الطلاب (شروط مركّبة) — استعلامات منفصلة
            var total = await _students.Query().AsNoTracking().CountAsync(s => !s.IsDeleted);
            var active = await _students.Query().AsNoTracking().CountAsync(s => s.status == StudentState.active && !s.IsDeleted);
            var left = await _students.Query().AsNoTracking().CountAsync(s => s.status == StudentState.left && !s.IsDeleted);

            // كل حالات الطلبات في استعلام GroupBy واحد بدل 8 استعلامات منفصلة
            var reqCounts = (await _requests.Query().AsNoTracking()
                    .GroupBy(r => r.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync())
                .Where(x => x.Status != null)
                .ToDictionary(x => x.Status!, x => x.Count);

            int Req(string status) => reqCounts.TryGetValue(status, out var c) ? c : 0;

            return new StudentStatsDto
            {
                total = total,
                active = active,
                left = left,
                submitted = Req("submitted"),
                housing_approved = Req("housing_approved"),
                housing_rejected = Req("housing_rejected"),
                cyber_review = Req("cyber_review"),
                cyber_approved = Req("cyber_approved"),
                cyber_rejected = Req("cyber_rejected"),
                ready_for_provisioning = Req("ready_for_provisioning"),
                completed = Req("completed"),
            };
        }

        public async Task<StudentDto> GetByIdAsync(int id)
        {
            var student = await _students.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");
            return Mapper.Map<StudentDto>(student);
        }

        public async Task<StudentDto> CreateAsync(StudentCreateDto dto)
        {
            EnsureNotReadonlyUser();

            var errors = Validate(dto.full_name, dto.full_name_english, dto.student_id, dto.national_id, dto.phone, dto.housing_building);
            if (await _students.ExistsAsync(s => s.student_id == dto.student_id && !s.IsDeleted))
                errors.Add("الرقم الجامعي موجود بالفعل");
            if (await _students.ExistsAsync(s => s.national_id == dto.national_id && !s.IsDeleted))
                errors.Add("رقم الهوية موجود بالفعل");
            if (errors.Count > 0)
                throw new UserFriendlyException(string.Join(" | ", errors), 400);

            var student = Mapper.Map<Student>(dto);
            student.created_at = DateTime.UtcNow;
            student.status ??= StudentState.active;
            student.student_status ??= StudentStatus.active;
            student.created_by = UnitOfWork.GetCurrentUserId();
            // gender اتحوّل لـ enum وبيتطبّع في الـ JsonConverter وقت الاستقبال — مفيش تطبيع يدوي محتاج هنا
            await _lookups.ApplyAsync(student); // FK ids من الأكواد (dual-write)

            try
            {
                await _students.AddAsync(student);
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
            {
                throw new UserFriendlyException("بيانات مكررة: رقم الهوية أو الرقم الجامعي موجود بالفعل", 409);
            }

            await _audit.LogAsync("create_student", "Students", student.Id);

            return Mapper.Map<StudentDto>(student);
        }

        public async Task<StudentDto> UpdateAsync(int id, StudentUpdateDto dto)
        {
            EnsureNotReadonlyUser();

            var student = await _students.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");

            var errors = Validate(dto.full_name, dto.full_name_english, dto.student_id, dto.national_id, dto.phone, dto.housing_building);
            if (errors.Count > 0)
                throw new UserFriendlyException(string.Join(" | ", errors), 400);

            // تفرّد رقم الهوية عند التعديل — مع استثناء الطالب نفسه.
            // (كان ناقص: التعديل ماكانش بيتأكد إن رقم الهوية مش مستخدم لطالب تاني — بق بيسمح بالتكرار.)
            if (!string.IsNullOrEmpty(dto.national_id) && dto.national_id != student.national_id
                && await _students.ExistsAsync(s => s.national_id == dto.national_id && s.Id != id && !s.IsDeleted))
                throw new UserFriendlyException("رقم الهوية موجود بالفعل لطالب آخر", 409);

            var changes = new List<AuditChangeLog>();
            var fields = new (string field, string? oldVal, string? newVal)[]
            {
                ("full_name", student.full_name, dto.full_name),
                ("full_name_english", student.full_name_english, dto.full_name_english),
                ("national_id", student.national_id, dto.national_id),
                ("phone", student.phone, dto.phone),
                ("gender", GenderHelper.ToStr(student.gender), GenderHelper.ToStr(dto.gender)),
                ("college", student.college, dto.college),
                ("department", student.department, dto.department),
                ("academic_level", student.academic_level, dto.academic_level),
                ("housing_building", student.housing_building, dto.housing_building),
                ("room_number", student.room_number, dto.room_number),
                ("apartment_number", student.apartment_number, dto.apartment_number),
                ("status", student.status?.ToString(), dto.status?.ToString()),
            };

            bool modified = false;
            foreach (var (field, oldVal, newVal) in fields)
            {
                if (!string.IsNullOrEmpty(newVal) && oldVal != newVal)
                {
                    if (field == "full_name" && !string.IsNullOrEmpty(dto.full_name)) student.full_name = dto.full_name;
                    else if (field == "full_name_english") student.full_name_english = dto.full_name_english;
                    else if (field == "national_id" && !string.IsNullOrEmpty(dto.national_id)) student.national_id = dto.national_id;
                    else if (field == "phone" && !string.IsNullOrEmpty(dto.phone)) student.phone = dto.phone;
                    else if (field == "gender" && dto.gender != null) student.gender = dto.gender;
                    else if (field == "college" && !string.IsNullOrEmpty(dto.college)) student.college = dto.college;
                    else if (field == "department") student.department = dto.department;
                    else if (field == "academic_level") student.academic_level = dto.academic_level;
                    else if (field == "housing_building" && !string.IsNullOrEmpty(dto.housing_building)) student.housing_building = dto.housing_building;
                    else if (field == "room_number" && !string.IsNullOrEmpty(dto.room_number)) student.room_number = dto.room_number;
                    else if (field == "apartment_number") student.apartment_number = dto.apartment_number;
                    else if (field == "status" && dto.status != null) student.status = dto.status;

                    changes.Add(new AuditChangeLog { FieldName = field, OldValue = oldVal, NewValue = newVal });
                    modified = true;
                }
            }

            if (!modified)
                return Mapper.Map<StudentDto>(student);

            await _lookups.ApplyAsync(student); // إعادة حساب الـ FK ids بعد تغيّر الأكواد

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
            {
                // خط دفاع تاني لو فيه unique index على رقم الهوية على مستوى الداتابيز
                throw new UserFriendlyException("بيانات مكررة: رقم الهوية موجود بالفعل", 409);
            }
            await _audit.LogAsync("update_student", "Students", student.Id, changes);

            return Mapper.Map<StudentDto>(student);
        }

        public async Task DeleteAsync(int id)
        {
            EnsureAdmin("غير مسموح لك بحذف الطالب. يرجى التواصل مع مسؤول النظام.");

            var student = await _students.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");
            if (student.IsDeleted)
                throw new UserFriendlyException("الطالب محذوف بالفعل", 400);

            var actorId = UnitOfWork.GetCurrentUserId();
            student.IsDeleted = true;
            student.DeletedBy = actorId > 0 ? actorId : (int?)null;
            student.DeletedDate = DateTime.UtcNow;

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("soft_delete_student", "Students", id);
        }

        public async Task RestoreAsync(int id)
        {
            EnsureAdmin("غير مسموح لك باستعادة الطالب. يرجى التواصل مع مسؤول النظام.");

            var student = await _students.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطالب غير موجود");
            if (!student.IsDeleted)
                throw new UserFriendlyException("الطالب غير محذوف", 400);

            var actorId = UnitOfWork.GetCurrentUserId();
            student.IsDeleted = false;
            student.RestoredBy = actorId > 0 ? actorId : (int?)null;
            student.RestoredDate = DateTime.UtcNow;
            student.DeletedBy = null;
            student.DeletedDate = null;

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("restore_student", "Students", id);
        }

        public async Task<List<LifecycleLogDto>> GetLifecycleAsync(int id)
        {
            return await _lifecycle.Query().AsNoTracking()
                .Where(l => l.StudentId == id)
                .OrderByDescending(l => l.PerformedAt)
                .Take(50)
                .Select(l => new LifecycleLogDto
                {
                    Id = l.Id,
                    Action = l.Action,
                    PerformedAt = l.PerformedAt,
                    Details = l.Details,
                    IpAddress = l.IpAddress,
                    PerformerName = l.Performer != null ? l.Performer.full_name : ""
                })
                .ToListAsync();
        }

        // ----------------------------- Helpers -----------------------------

        // user (قراءة فقط) ممنوع من الإضافة/التعديل — نفس منطق الكنترولر القديم
        private void EnsureNotReadonlyUser()
        {
            var role = UnitOfWork.GetCurrentUserRole()?.ToLower();
            if (role == "user")
                throw UserFriendlyException.Forbidden();
        }

        // الحذف/الاستعادة للأدمن فقط
        private void EnsureAdmin(string message)
        {
            var role = UnitOfWork.GetCurrentUserRole()?.ToLower();
            if (role != "admin")
                throw UserFriendlyException.Forbidden(message);
        }

        private static List<string> Validate(string? fullName, string? fullNameEn, string? studentId, string? nationalId, string? phone, string? housingBuilding)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(fullName) || !Regex.IsMatch(fullName, @"^[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\s]+$"))
                errors.Add("الاسم بالعربية: يرجى إدخال الاسم باللغة العربية فقط");
            if (string.IsNullOrEmpty(fullNameEn) || !Regex.IsMatch(fullNameEn, @"^[a-zA-Z\s]+$"))
                errors.Add("الاسم بالإنجليزية: يرجى إدخال الاسم باللغة الإنجليزية فقط");
            if (string.IsNullOrEmpty(studentId) || !Regex.IsMatch(studentId, @"^\d{9,10}$"))
                errors.Add("الرقم الجامعي: يجب أن يتكون الرقم الجامعي من 9 أو 10 أرقام");
            if (string.IsNullOrEmpty(nationalId) || !Regex.IsMatch(nationalId, @"^\d{10}$"))
                errors.Add("رقم الهوية: يجب أن يتكون رقم الهوية من 10 أرقام");
            if (!string.IsNullOrEmpty(phone) && !Regex.IsMatch(phone, @"^9665\d{8}$"))
                errors.Add("رقم الجوال: يجب أن يبدأ الرقم بـ 9665 ويتكون من 12 رقمًا");
            if (!string.IsNullOrEmpty(housingBuilding) && !ValidBuildings.Contains(housingBuilding))
                errors.Add("رقم المبنى السكني غير صحيح - القيم المسموح بها: 40,41,42,43,65,66,67,68,69,70");
            return errors;
        }
    }
}
