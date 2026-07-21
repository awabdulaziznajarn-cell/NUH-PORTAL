using MapsterMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Housing;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // نقل سكن الطلاب بواسطة المشرف — اتنقل من SupervisorHousingTransferController
    public class SupervisorHousingTransferService : AppServiceBase, ISupervisorHousingTransferService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<HousingTransfer> _transfers;
        private readonly IRepository<AccountLifecycleLog> _lifecycle;
        private readonly IAuditService _audit;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _http;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".docx"
        };

        public const long MaxFileSize = 10 * 1024 * 1024;

        public SupervisorHousingTransferService(
            IRepository<Student> students,
            IRepository<HousingTransfer> transfers,
            IRepository<AccountLifecycleLog> lifecycle,
            IAuditService audit,
            IWebHostEnvironment env,
            IHttpContextAccessor http,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _transfers = transfers;
            _lifecycle = lifecycle;
            _audit = audit;
            _env = env;
            _http = http;
        }

        public async Task<TransferResultDto> CreateTransferAsync(string studentNumber, string newBuilding, string newApartment, string newRoom, string reason, string? customReason, IFormFile? file)
        {
            if (UnitOfWork.GetCurrentUserRole()?.ToLower() != "supervisor")
                throw UserFriendlyException.Forbidden();

            if (string.IsNullOrEmpty(studentNumber))
                throw new UserFriendlyException("الرقم الجامعي مطلوب", 400);
            if (string.IsNullOrEmpty(newBuilding))
                throw new UserFriendlyException("رقم المبنى الجديد مطلوب", 400);
            if (string.IsNullOrEmpty(newApartment))
                throw new UserFriendlyException("رقم الشقة الجديدة مطلوب", 400);
            if (string.IsNullOrEmpty(newRoom))
                throw new UserFriendlyException("رقم الغرفة الجديدة مطلوب", 400);
            if (string.IsNullOrEmpty(reason))
                throw new UserFriendlyException("سبب النقل مطلوب", 400);
            if (reason == "other" && string.IsNullOrEmpty(customReason))
                throw new UserFriendlyException("يرجى كتابة وصف السبب", 400);

            var student = await _students.FindAsync(s => s.student_id == studentNumber && !s.IsDeleted)
                ?? throw new UserFriendlyException("الطالب غير موجود", 400);

            if (string.IsNullOrEmpty(student.housing_building) && string.IsNullOrEmpty(student.room_number) && string.IsNullOrEmpty(student.apartment_number))
                throw new UserFriendlyException("الطالب لا يمتلك بيانات سكن حالية", 400);

            var oldBuilding = student.housing_building ?? "";
            var oldApartment = student.apartment_number ?? "";
            var oldRoom = student.room_number ?? "";

            if (file != null)
            {
                var fileExt = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(fileExt) || !AllowedTypes.Contains(fileExt))
                    throw new UserFriendlyException($"نوع الملف {fileExt} غير مسموح به. الصيغ المسموحة: PDF, JPG, JPEG, PNG, DOCX", 400);
                if (file.Length > MaxFileSize)
                    throw new UserFriendlyException("حجم الملف يتجاوز 10 ميجابايت", 400);
            }

            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            var clientIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

            var transfer = new HousingTransfer
            {
                StudentId = student.Id,
                StudentNumber = student.student_id,
                OldBuilding = oldBuilding,
                OldApartment = oldApartment,
                OldRoom = oldRoom,
                NewBuilding = newBuilding,
                NewApartment = newApartment,
                NewRoom = newRoom,
                Reason = reason,
                CustomReason = reason == "other" ? customReason : null,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            };

            await _transfers.AddAsync(transfer);
            await UnitOfWork.SaveAsync();

            student.housing_building = newBuilding;
            student.apartment_number = newApartment;
            student.room_number = newRoom;

            var reasonAr = reason switch
            {
                "student_request" => "طلب الطالب",
                "major_change" => "تغيير تخصص",
                "housing_issue" => "مشكلة سكنية",
                "room_maintenance" => "صيانة غرفة",
                "admin_decision" => "قرار الإدارة",
                "other" => customReason ?? "أخرى",
                _ => reason
            };

            await _lifecycle.AddAsync(new AccountLifecycleLog
            {
                StudentId = student.Id,
                Action = "housing_transfer",
                PerformedBy = actorId,
                PerformedAt = DateTime.UtcNow,
                Details = $"نقل سكن طالب: {oldBuilding}/{oldApartment}/{oldRoom} -> {newBuilding}/{newApartment}/{newRoom} - السبب: {reasonAr}",
                IpAddress = clientIp
            });

            // حفظ المرفق (لو فيه)
            if (file != null)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "housing-transfer", transfer.Id.ToString());
                Directory.CreateDirectory(uploadDir);

                var ext = Path.GetExtension(file.FileName);
                var storedFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadDir, storedFileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                transfer.AttachmentPath = storedFileName;
                transfer.OriginalFileName = file.FileName;
            }

            await UnitOfWork.SaveAsync();
            await _audit.LogAsync("housing_transfer", "HousingTransfers", transfer.Id);

            return new TransferResultDto
            {
                Message = "تم نقل الطالب بنجاح",
                TransferId = transfer.Id,
                OldLocation = $"{oldBuilding}/{oldApartment}/{oldRoom}",
                NewLocation = $"{newBuilding}/{newApartment}/{newRoom}"
            };
        }

        public async Task<List<RecentTransferDto>> GetRecentAsync()
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId == 0)
                throw new UserFriendlyException("غير مصرح", 401);

            // ملحوظة: ضفنا Include(Student) — الكود القديم كان بيرجّع StudentName = null دايمًا (باج)
            return await _transfers.Query().AsNoTracking()
                .Include(t => t.Student)
                .Where(t => t.CreatedBy == actorId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .Select(t => new RecentTransferDto
                {
                    Id = t.Id,
                    StudentNumber = t.StudentNumber,
                    OldBuilding = t.OldBuilding,
                    OldApartment = t.OldApartment,
                    OldRoom = t.OldRoom,
                    NewBuilding = t.NewBuilding,
                    NewApartment = t.NewApartment,
                    NewRoom = t.NewRoom,
                    Reason = t.Reason,
                    CustomReason = t.CustomReason,
                    CreatedAt = t.CreatedAt,
                    CreatedBy = t.CreatedBy,
                    AttachmentPath = t.AttachmentPath,
                    OriginalFileName = t.OriginalFileName,
                    StudentName = t.Student != null ? t.Student.full_name : null
                })
                .ToListAsync();
        }
    }
}
