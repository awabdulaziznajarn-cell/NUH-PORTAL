using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Tracking;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class RequestTrackingService : AppServiceBase, IRequestTrackingService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<Request> _requests;
        private readonly IWorkflowService _workflow;

        public RequestTrackingService(
            IRepository<Student> students,
            IRepository<Request> requests,
            IWorkflowService workflow,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _requests = requests;
            _workflow = workflow;
        }

        public async Task<List<TrackedRequestDto>> TrackByMobileAsync(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                throw new UserFriendlyException("رقم الجوال مطلوب", 400);

            var normalized = NormalizePhone(mobile);

            var studentIds = await _students.Query().AsNoTracking()
                .Where(s => s.phone == mobile || s.phone == normalized)
                .Select(s => s.Id)
                .ToListAsync();

            if (studentIds.Count == 0)
                throw UserFriendlyException.NotFound("لا توجد طلبات مرتبطة بهذا الرقم");

            return await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .Where(r => r.RequestType == "self_registration" && studentIds.Contains(r.StudentId))
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new TrackedRequestDto
                {
                    RequestNumber = r.RequestNumber,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    StudentName = r.Student!.full_name
                })
                .ToListAsync();
        }

        public async Task<TrackingDetailsDto> TrackByNumberAsync(string requestNumber)
        {
            if (string.IsNullOrWhiteSpace(requestNumber))
                throw new UserFriendlyException("رقم الطلب مطلوب", 400);

            var request = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber && r.RequestType == "self_registration")
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            var history = await _workflow.GetHistoryAsync(request.Id);

            return new TrackingDetailsDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                Status = request.Status,
                SubmittedAt = request.SubmittedAt,
                StudentName = request.Student?.full_name,
                AdUsername = request.Student?.ad_username,
                History = history.Select(h => new TrackingHistoryItemDto
                {
                    ToStage = h.ToStage,
                    ActionDate = h.ActionDate,
                    Notes = h.Notes,
                    ActorName = h.Actor?.full_name ?? h.Actor?.username
                }).ToList()
            };
        }

        // ----------------------------- Helpers -----------------------------

        // 05XXXXXXXX أو 5XXXXXXXX → 9665XXXXXXXX (نفس منطق الكنترولر القديم)
        private static string NormalizePhone(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return mobile;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 10 && digits.StartsWith("05"))
                return "9665" + digits[2..];
            if (digits.Length == 9 && digits.StartsWith("5"))
                return "966" + digits;
            return digits;
        }
    }
}
