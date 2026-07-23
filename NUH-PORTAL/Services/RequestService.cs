using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Requests;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // كل منطق دورة حياة الطلب: إنشاء → مراجعة إسكان → مراجعة إلكترونية → تجهيز → إكمال (مع AD provisioning)
    public class RequestService : AppServiceBase, IRequestService
    {
        private readonly IRepository<Request> _requests;
        private readonly IRepository<Student> _students;
        private readonly IRepository<User> _users;
        private readonly IRepository<Notification> _notifications;
        private readonly ADProvisioningService _adProvisioning;
        private readonly IAuditService _audit;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<RequestService> _logger;

        public RequestService(
            IRepository<Request> requests,
            IRepository<Student> students,
            IRepository<User> users,
            IRepository<Notification> notifications,
            ADProvisioningService adProvisioning,
            IAuditService audit,
            IHttpContextAccessor http,
            ILogger<RequestService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _requests = requests;
            _students = students;
            _users = users;
            _notifications = notifications;
            _adProvisioning = adProvisioning;
            _audit = audit;
            _http = http;
            _logger = logger;
        }

        public async Task<List<RequestDto>> GetAllAsync()
        {
            var list = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .ToListAsync();
            return Mapper.Map<List<RequestDto>>(list);
        }


        public async Task<QueryResult<RequestDto>> GetPagedAsync(QueryParams queryParams, string? status, string? requestType)
        {
            var query = _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                // "rejected" حالة مجمّعة لتبويب المرفوض — بتجمع رفض الإسكان ورفض الأمن السيبراني
                if (status == "rejected")
                    query = query.Where(r => r.Status == "housing_rejected" || r.Status == "cyber_rejected");
                else
                    query = query.Where(r => r.Status == status);
            }
            if (!string.IsNullOrEmpty(requestType) && Enum.TryParse<RequestType>(requestType, out var rt))
                query = query.Where(r => r.RequestType == rt);

            var f = queryParams.FilterText?.Trim();
            if (!string.IsNullOrEmpty(f))
            {
                query = query.Where(r =>
                    (r.RequestNumber != null && r.RequestNumber.Contains(f)) ||
                    (r.Student != null && r.Student.full_name != null && r.Student.full_name.Contains(f)) ||
                    (r.Student != null && r.Student.student_id != null && r.Student.student_id.Contains(f)));
            }

            query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortAsc) switch
            {
                ("id", true) => query.OrderBy(r => r.Id),
                ("id", false) => query.OrderByDescending(r => r.Id),
                ("status", true) => query.OrderBy(r => r.Status),
                ("status", false) => query.OrderByDescending(r => r.Status),
                ("requestnumber", true) => query.OrderBy(r => r.RequestNumber),
                ("requestnumber", false) => query.OrderByDescending(r => r.RequestNumber),
                ("submittedat", true) => query.OrderBy(r => r.SubmittedAt),
                _ => query.OrderByDescending(r => r.SubmittedAt)
            };

            var result = await query.ToPagedResultAsync(queryParams);
            return result.Map<Request, RequestDto>(Mapper);
        }

        public async Task<RequestStatsDto> GetStatsAsync()
        {
            // عدّة واحدة على السيرفر (GroupBy) بدل تحميل كل الطلبات وعدّها في المتصفح
            var counts = await _requests.Query().AsNoTracking()
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int Of(string s) => counts.Where(c => c.Status == s).Sum(c => c.Count);

            return new RequestStatsDto
            {
                Total = counts.Sum(c => c.Count),
                Submitted = Of("submitted"),
                HousingApproved = Of("housing_approved"),
                CyberReview = Of("cyber_review"),
                CyberApproved = Of("cyber_approved"),
                ReadyForProvisioning = Of("ready_for_provisioning"),
                Completed = Of("completed"),
                Rejected = Of("housing_rejected") + Of("cyber_rejected")
            };
        }

        public async Task<RequestDetailsDto> GetDetailsAsync(int id)
        {
            var request = await _requests.Query().AsNoTracking()
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            // تجميع أسماء المستخدمين اللي شاركوا في الطلب في استعلام واحد
            var userIds = new HashSet<int>();
            if (request.SubmittedBy.HasValue) userIds.Add(request.SubmittedBy.Value);
            if (request.HousingReviewedBy.HasValue) userIds.Add(request.HousingReviewedBy.Value);
            if (request.CyberReviewedBy.HasValue) userIds.Add(request.CyberReviewedBy.Value);
            if (request.ReadyForProvisioningBy.HasValue) userIds.Add(request.ReadyForProvisioningBy.Value);
            if (request.CompletedBy.HasValue) userIds.Add(request.CompletedBy.Value);

            var userNames = userIds.Count > 0
                ? await _users.Query().AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.full_name ?? u.username ?? "Unknown")
                : new Dictionary<int, string>();

            string? NameOf(int? userId) =>
                userId.HasValue && userNames.TryGetValue(userId.Value, out var n) ? n : null;

            var dto = new RequestDetailsDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                RequestType = request.RequestType,
                StudentId = request.StudentId,
                Status = request.Status,
                Notes = request.Notes,
                SubmittedAt = request.SubmittedAt,
                ReviewedAt = request.ReviewedAt,
                ReviewedBy = request.ReviewedBy,
                HousingReviewedAt = request.HousingReviewedAt,
                HousingReviewedBy = request.HousingReviewedBy,
                HousingNotes = request.HousingNotes,
                CyberReviewedAt = request.CyberReviewedAt,
                CyberReviewedBy = request.CyberReviewedBy,
                CyberNotes = request.CyberNotes,
                ReadyForProvisioningAt = request.ReadyForProvisioningAt,
                ReadyForProvisioningBy = request.ReadyForProvisioningBy,
                CompletedAt = request.CompletedAt,
                CompletedBy = request.CompletedBy,
                BulkRequestId = request.BulkRequestId,
                RequestedByRole = request.RequestedByRole,
                Student = Mapper.Map<NUH_PORTAL.DTOs.Students.StudentDto>(request.Student),
                SubmittedByName = NameOf(request.SubmittedBy),
                HousingReviewedByName = NameOf(request.HousingReviewedBy),
                CyberReviewedByName = NameOf(request.CyberReviewedBy),
                ReadyForProvisioningByName = NameOf(request.ReadyForProvisioningBy),
                CompletedByName = NameOf(request.CompletedBy)
            };
            return dto;
        }

        public async Task<List<RequestDto>> GetPendingAsync()
        {
            var list = await _requests.Query().AsNoTracking()
                .Where(r => r.Status == "submitted")
                .ToListAsync();
            return Mapper.Map<List<RequestDto>>(list);
        }

        public async Task<RequestDto> CreateAsync(RequestCreateDto dto)
        {
            var role = UnitOfWork.GetCurrentUserRole()?.ToLower();
            if (role == "user")
                throw UserFriendlyException.Forbidden();

            var actorId = UnitOfWork.GetCurrentUserId();

            var request = Mapper.Map<Request>(dto);

            // مشرف/أدمن بينشئ الطلب → موافقة الإسكان تلقائيًا والتحويل مباشرة للمراجعة الإلكترونية
            var isHousingCreator = role == "supervisor" || role == "admin";
            request.Status = isHousingCreator ? "cyber_review" : "submitted";
            request.SubmittedBy = actorId > 0 ? actorId : null;
            request.SubmittedAt = DateTime.UtcNow;
            request.RequestedByRole = role;

            if (isHousingCreator)
            {
                request.HousingReviewedBy = actorId;
                request.HousingReviewedAt = DateTime.UtcNow;
                request.ReviewedBy = actorId;
                request.ReviewedAt = DateTime.UtcNow;
            }

            try
            {
                await _requests.AddAsync(request);
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547 && sqlEx.Message.Contains("FOREIGN KEY"))
                    throw new UserFriendlyException("الطالب غير موجود", 409);
                if (sqlEx.Number == 547 && sqlEx.Message.Contains("CHECK"))
                    throw new UserFriendlyException("بيانات الطلب غير صالحة", 400);
                throw;
            }

            // نفس أسماء أحداث الـ audit القديمة بالحرف (متسجلة كده في تقارير الـ AuditLogs)
            await _audit.LogAsync("housing_approve_request", "Requests", request.Id);
            if (isHousingCreator)
                await _audit.LogAsync("submit_cyber_review", "Requests", request.Id);

            var reqNum = request.RequestNumber ?? $"{DateTime.UtcNow.Year}-{request.Id:D6}";
            var notifRoles = isHousingCreator ? new[] { "cyber" } : new[] { "admin", "supervisor", "cyber" };
            var notifMsg = isHousingCreator
                ? $"تم تقديم طلب جديد وإحالته للمراجعة الإلكترونية ({reqNum})"
                : $"تم تقديم طلب جديد ({reqNum})";
            await NotifyRolesAsync(request.Id, notifRoles, notifMsg);

            return Mapper.Map<RequestDto>(request);
        }

        public async Task<RequestDto> ReviewAsync(int id, ReviewDto dto)
        {
            var req = await _requests.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            var actorRole = UnitOfWork.GetCurrentUserRole()?.ToLower();
            var actorId = UnitOfWork.GetCurrentUserId();
            var oldStatus = req.Status;

            // جدول الانتقالات المسموحة: (الدور، الحالة الحالية، الحالة الجديدة)
            var allowed = (actorRole, req.Status, dto.Status) switch
            {
                ("admin" or "supervisor", "submitted", "housing_approved" or "housing_rejected") => true,
                ("admin", "housing_approved", "cyber_review") => true,
                ("admin" or "cyber", "cyber_review", "cyber_approved" or "cyber_rejected") => true,
                ("admin" or "cyber", "cyber_approved", "ready_for_provisioning") => true,
                ("admin", "ready_for_provisioning", "completed") => true,
                _ => false
            };

            if (!allowed)
                throw new UserFriendlyException("Transition not allowed for this role", 400);

            req.Status = dto.Status;
            req.Notes = dto.Notes;

            Student? student = null;

            if (dto.Status == "housing_approved" || dto.Status == "housing_rejected")
            {
                req.HousingReviewedBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.HousingReviewedAt = DateTime.UtcNow;
                req.HousingNotes = dto.Notes;
            }
            else if (dto.Status == "cyber_approved" || dto.Status == "cyber_rejected")
            {
                req.CyberReviewedBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.CyberReviewedAt = DateTime.UtcNow;
                req.CyberNotes = dto.Notes;
            }
            else if (dto.Status == "ready_for_provisioning")
            {
                req.ReadyForProvisioningBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.ReadyForProvisioningAt = DateTime.UtcNow;
            }
            else if (dto.Status == "completed")
            {
                student = await _students.GetByIdAsync(req.StudentId);
                if (student != null)
                {
                    var ctx = _http.HttpContext;
                    var ip = ctx?.Connection.RemoteIpAddress?.ToString();
                    var ua = ctx?.Request.Headers["User-Agent"].ToString();

                    var provResult = await _adProvisioning.ProvisionAsync(student, actorId, ip, ua);
                    if (!provResult.Success)
                    {
                        _logger.LogError("AD provisioning FAILED for student {Id}: {Error} | StackTrace: {Stack}",
                            student.student_id, provResult.Error, provResult.StackTrace);
                        // الرسالة فيها سبب الفشل — من غير stack trace للعميل (كان بيتسرب قبل كده)
                        throw new UserFriendlyException($"فشل إنشاء حساب الشبكة — لم يتم إكمال الطلب: {provResult.Error}", 500);
                    }

                    _logger.LogInformation("AD account created for student {Id}: {Sam}", student.student_id, provResult.SamAccountName);
                    var syncResult = await _adProvisioning.SyncExtensionAttributesAsync(student, actorId);
                    if (!syncResult.Success)
                        _logger.LogWarning("Extension attribute sync failed for student {Id}: {Error}", student.student_id, syncResult.Error);
                }

                req.CompletedBy = dto.ReviewedBy > 0 ? dto.ReviewedBy : actorId;
                req.CompletedAt = DateTime.UtcNow;
                if (student != null && student.status != StudentState.left)
                    student.status = StudentState.active;
            }

            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedBy = actorId;

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547)
                    throw new UserFriendlyException("خطأ في تحديث الطلب", 409);
                throw;
            }

            var reviewAction = dto.Status switch
            {
                "housing_approved" => "housing_approve_request",
                "housing_rejected" => "housing_reject_request",
                "cyber_review" => "submit_cyber_review",
                "cyber_approved" => "cyber_approve_request",
                "cyber_rejected" => "cyber_reject_request",
                "ready_for_provisioning" => "ready_for_provisioning_request",
                "bulk_registration_completed" => "bulk_registration_completed",
                "completed" => "complete_request",
                _ => null
            };

            if (reviewAction != null)
            {
                var changes = new List<AuditChangeLog>();
                if (oldStatus != dto.Status)
                    changes.Add(new AuditChangeLog { FieldName = "Status", OldValue = oldStatus, NewValue = dto.Status });

                await _audit.LogAsync(reviewAction, "Requests", req.Id, changes.Count > 0 ? changes : null);

                var reqNum = req.RequestNumber ?? $"{DateTime.UtcNow.Year}-{req.Id:D6}";
                var notifRoles = dto.Status switch
                {
                    "housing_approved" => new[] { "admin", "cyber" },
                    "housing_rejected" => new[] { "admin", req.RequestedByRole ?? "supervisor" },
                    "cyber_review" => new[] { "cyber" },
                    "cyber_approved" => new[] { "admin", "supervisor", req.RequestedByRole ?? "admin" },
                    "cyber_rejected" => new[] { req.RequestedByRole ?? "admin" },
                    "ready_for_provisioning" => new[] { "admin", "supervisor" },
                    "completed" => new[] { "admin", "supervisor", req.RequestedByRole ?? "admin" },
                    _ => Array.Empty<string>()
                };
                var notifMsg = dto.Status switch
                {
                    "housing_approved" => $"تمت الموافقة على الطلب ({reqNum}) من قبل لجنة الإسكان",
                    "housing_rejected" => $"تم رفض الطلب ({reqNum}) من قبل لجنة الإسكان",
                    "cyber_review" => $"تم إحالة الطلب ({reqNum}) إلى المراجعة الإلكترونية",
                    "cyber_approved" => $"تمت الموافقة الإلكترونية على الطلب ({reqNum})",
                    "cyber_rejected" => $"تم الرفض الإلكتروني للطلب ({reqNum})",
                    "ready_for_provisioning" => $"الطلب ({reqNum}) جاهز لإنشاء حساب شبكة السكن",
                    "completed" => $"تم إكمال الطلب ({reqNum})",
                    _ => ""
                };
                await NotifyRolesAsync(req.Id, notifRoles, notifMsg);
            }

            return Mapper.Map<RequestDto>(req);
        }

        public async Task<RequestDto> UpdateBulkIdAsync(int id, UpdateRequestDto dto)
        {
            var req = await _requests.GetByIdAsync(id)
                ?? throw UserFriendlyException.NotFound("الطلب غير موجود");

            if (dto.BulkRequestId.HasValue)
                req.BulkRequestId = dto.BulkRequestId.Value;

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
            {
                if (sqlEx.Number == 547)
                    throw new UserFriendlyException("خطأ في تحديث الطلب", 409);
                throw;
            }

            return Mapper.Map<RequestDto>(req);
        }

        // ----------------------------- Helpers -----------------------------

        private async Task NotifyRolesAsync(int requestId, string[] roles, string message)
        {
            if (roles.Length == 0) return;

            foreach (var r in roles)
            {
                await _notifications.AddAsync(new Notification
                {
                    request_id = requestId,
                    channel = "in_app",
                    recipient_role = r,
                    message = message,
                    status = "pending",
                    sent_at = DateTime.UtcNow
                });
            }

            try
            {
                await UnitOfWork.SaveAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
            {
                throw new UserFriendlyException("خطأ في الإشعارات", 409);
            }
        }
    }
}
