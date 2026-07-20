using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace NUH_PORTAL.Controllers
{
    [Authorize(Roles = "admin,supervisor")]
    [Route("api/[controller]")]
    [ApiController]
    public class BulkRegistrationController : ControllerBase
    {
        private readonly AppDbContext _context;
        public BulkRegistrationController(AppDbContext context) => _context = context;

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Template");
            var headers = new[] { "StudentID", "NationalID", "FullNameArabic", "FullNameEnglish", "Mobile", "College", "Department", "AcademicLevel", "Gender", "BuildingNumber", "ApartmentNumber", "RoomNumber" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BulkRegistrationTemplate.xlsx");
        }

        public class UploadResultDto
        {
            public int TotalRecords { get; set; }
            public int ValidRecords { get; set; }
            public int ErrorRecords { get; set; }
            public List<RowErrorDto>? Errors { get; set; }
            public List<PreviewDto>? Preview { get; set; }
            public string? FileName { get; set; }
        }

        public class RowErrorDto
        {
            public int Row { get; set; }
            public string StudentID { get; set; } = string.Empty;
            public string ColumnName { get; set; } = string.Empty;
            public string ErrorDescription { get; set; } = string.Empty;
        }

        public class PreviewDto
        {
            public string StudentID { get; set; } = string.Empty;
            public string NationalID { get; set; } = string.Empty;
            public string FullNameArabic { get; set; } = string.Empty;
            public string FullNameEnglish { get; set; } = string.Empty;
            public string Mobile { get; set; } = string.Empty;
            public string College { get; set; } = string.Empty;
            public string? Department { get; set; }
            public string? AcademicLevel { get; set; }
            public string? Gender { get; set; }
            public string? BuildingNumber { get; set; }
            public string? ApartmentNumber { get; set; }
            public string? RoomNumber { get; set; }
        }

        [HttpPost("validate")]
        [RequestSizeLimit(10L * 1024 * 1024)]
        public async Task<ActionResult<UploadResultDto>> ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "الرجاء رفع ملف" });
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "يُسمح فقط بملفات Excel (.xlsx)" });
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { error = "حجم الملف يجب أن لا يتجاوز 10 ميجابايت" });

            DataTable dt;
            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheet(1);
                dt = new DataTable();
                var firstRow = ws.FirstRowUsed()?.CellsUsed().ToList() ?? new();
                foreach (var cell in firstRow)
                    dt.Columns.Add(cell.Value.ToString().Trim());

                var rows = ws.RangeUsed()?.RowsUsed().Skip(1).ToList() ?? new();
                foreach (var row in rows)
                {
                    var dr = dt.NewRow();
                    var cells = row.Cells().ToList();
                    for (int i = 0; i < dt.Columns.Count && i < cells.Count; i++)
                        dr[i] = cells[i].Value.ToString().Trim();
                    dt.Rows.Add(dr);
                }
            }
            catch
            {
                return BadRequest(new { error = "فشل قراءة الملف. تأكد من صيغة الملف." });
            }

            var errors = new List<RowErrorDto>();
            var preview = new List<PreviewDto>();
            int validCount = 0, errorCount = 0;
            var processedStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var existingStudentIds = new HashSet<string>(
                await _context.Students.Where(s => s.student_id != null).Select(s => s.student_id!).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                var row = dt.Rows[i];
                var studentId = row["StudentID"]?.ToString()?.Trim() ?? "";
                var nationalId = row["NationalID"]?.ToString()?.Trim() ?? "";
                var fullNameAr = row["FullNameArabic"]?.ToString()?.Trim() ?? "";
                var fullNameEn = row["FullNameEnglish"]?.ToString()?.Trim() ?? "";
                var mobile = row["Mobile"]?.ToString()?.Trim() ?? "";
                var gender = row["Gender"]?.ToString()?.Trim() ?? "";
                var buildingNumber = row["BuildingNumber"]?.ToString()?.Trim() ?? "";
                var apartmentNumber = row["ApartmentNumber"]?.ToString()?.Trim() ?? "";
                var roomNumber = row["RoomNumber"]?.ToString()?.Trim() ?? "";
                var academicLevel = row["AcademicLevel"]?.ToString()?.Trim() ?? "";
                var college = row["College"]?.ToString()?.Trim() ?? "";
                var department = row["Department"]?.ToString()?.Trim() ?? "";

                bool rowHasError = false;

                if (string.IsNullOrEmpty(studentId))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "StudentID", ErrorDescription = "الرقم الجامعي مطلوب - يجب إدخال رقم جامعي مكون من 9-10 أرقام" });
                }
                else if (!Regex.IsMatch(studentId, @"^\d{9,10}$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "StudentID", ErrorDescription = "الرقم الجامعي غير صحيح - يجب أن يتكون من 9 إلى 10 أرقام" });
                }
                else if (existingStudentIds.Contains(studentId))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "StudentID", ErrorDescription = "الرقم الجامعي موجود مسبقاً في النظام - لا يمكن تكرار الرقم الجامعي" });
                }
                else if (processedStudentIds.Contains(studentId))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "StudentID", ErrorDescription = "الرقم الجامعي مكرر في الملف - تم إدخال نفس الرقم الجامعي في أكثر من سطر" });
                }

                if (string.IsNullOrEmpty(nationalId))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "NationalID", ErrorDescription = "رقم الهوية الوطنية مطلوب - يجب إدخال رقم هوية مكون من 10 أرقام" });
                }
                else if (!Regex.IsMatch(nationalId, @"^\d{10}$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "NationalID", ErrorDescription = "رقم الهوية الوطنية غير صحيح - يجب أن يتكون من 10 أرقام بالضبط" });
                }

                if (string.IsNullOrEmpty(fullNameAr))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "FullNameArabic", ErrorDescription = "الاسم العربي مطلوب - يجب إدخال الاسم باللغة العربية" });
                }
                else if (!Regex.IsMatch(fullNameAr, @"^[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\s]+$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "FullNameArabic", ErrorDescription = "الاسم العربي يحتوي على أحرف غير عربية - يجب إدخال الاسم باللغة العربية فقط" });
                }

                if (string.IsNullOrEmpty(fullNameEn))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "FullNameEnglish", ErrorDescription = "الاسم الإنجليزي مطلوب - يجب إدخال الاسم باللغة الإنجليزية" });
                }
                else if (!Regex.IsMatch(fullNameEn, @"^[a-zA-Z\s]+$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "FullNameEnglish", ErrorDescription = "الاسم الإنجليزي يحتوي على أحرف غير إنجليزية - يجب إدخال الاسم باللغة الإنجليزية فقط" });
                }

                if (string.IsNullOrEmpty(mobile))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "Mobile", ErrorDescription = "رقم الجوال مطلوب - يجب إدخال رقم جوال صحيح" });
                }
                else if (!Regex.IsMatch(mobile, @"^9665\d{8}$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "Mobile", ErrorDescription = "رقم الجوال غير صحيح - يجب أن يبدأ بـ 9665 ويتكون من 12 رقماً (مثال: 9665XXXXXXXX)" });
                }

                if (!string.IsNullOrEmpty(gender) && !GenderHelper.IsValid(gender))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "Gender", ErrorDescription = "قيمة الجنس غير صحيحة - القيم المسموح بها: ذكر, أنثى, Male, Female" });
                }

                if (!string.IsNullOrEmpty(academicLevel) && !Regex.IsMatch(academicLevel, @"^[1-5]$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "AcademicLevel", ErrorDescription = "المستوى الدراسي غير صحيح - يجب أن يكون رقماً بين 1 و 5" });
                }

                // BuildingNumber validation
                if (string.IsNullOrEmpty(buildingNumber))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "BuildingNumber", ErrorDescription = "رقم المبنى السكني مطلوب" });
                }
                else if (!Regex.IsMatch(buildingNumber, @"^(4[0-3]|6[5-9]|70)$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "BuildingNumber", ErrorDescription = "رقم المبنى السكني غير صحيح - القيم المسموح بها: 40,41,42,43,65,66,67,68,69,70" });
                }

                // ApartmentNumber validation
                if (string.IsNullOrEmpty(apartmentNumber))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "ApartmentNumber", ErrorDescription = "رقم الشقة مطلوب" });
                }
                else if (!Regex.IsMatch(apartmentNumber, @"^\d+$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "ApartmentNumber", ErrorDescription = "رقم الشقة غير صحيح - يجب أن يكون رقماً فقط" });
                }

                // RoomNumber validation
                if (string.IsNullOrEmpty(roomNumber))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "RoomNumber", ErrorDescription = "رقم الغرفة مطلوب" });
                }
                else if (!Regex.IsMatch(roomNumber, @"^\d+$"))
                {
                    rowHasError = true;
                    errors.Add(new RowErrorDto { Row = i + 2, StudentID = studentId, ColumnName = "RoomNumber", ErrorDescription = "رقم الغرفة غير صحيح - يجب أن يكون رقماً فقط" });
                }

                processedStudentIds.Add(studentId);

                if (rowHasError)
                {
                    errorCount++;
                }
                else
                {
                    validCount++;
                    preview.Add(new PreviewDto
                    {
                        StudentID = studentId,
                        NationalID = nationalId,
                        FullNameArabic = fullNameAr,
                        FullNameEnglish = fullNameEn,
                        Mobile = mobile,
                        College = college,
                        Department = department,
                        AcademicLevel = academicLevel,
                        Gender = gender,
                        BuildingNumber = buildingNumber,
                        ApartmentNumber = apartmentNumber,
                        RoomNumber = roomNumber
                    });
                }
            }

            return Ok(new UploadResultDto
            {
                TotalRecords = dt.Rows.Count,
                ValidRecords = validCount,
                ErrorRecords = errorCount,
                Errors = errors,
                Preview = preview,
                FileName = file.FileName
            });
        }

        public class BulkCreateDto
        {
            public string FileName { get; set; } = string.Empty;
            public List<BulkStudentDto> Students { get; set; } = new();
        }

        public class BulkStudentDto
        {
            public string StudentID { get; set; } = string.Empty;
            public string NationalID { get; set; } = string.Empty;
            public string FullNameArabic { get; set; } = string.Empty;
            public string FullNameEnglish { get; set; } = string.Empty;
            public string Mobile { get; set; } = string.Empty;
            public string? College { get; set; }
            public string? Department { get; set; }
            public string? AcademicLevel { get; set; }
            public string? Gender { get; set; }
            public string? BuildingNumber { get; set; }
            public string? ApartmentNumber { get; set; }
            public string? RoomNumber { get; set; }
        }

        [HttpPost("create")]
        public async Task<ActionResult> CreateBulkRequest([FromBody] BulkCreateDto dto)
        {
            var actorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (actorId <= 0)
                return Unauthorized();

            if (dto.Students == null || dto.Students.Count == 0)
                return BadRequest(new { error = "لا يوجد طلاب صالحون للتسجيل" });

            var role = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
            var isHousingCreator = role == "admin" || role == "supervisor";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Pre-check: reject any duplicate student IDs within the same batch
                var dupInBatch = dto.Students
                    .Where(s => !string.IsNullOrEmpty(s.StudentID))
                    .GroupBy(s => s.StudentID, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (dupInBatch.Count > 0)
                {
                    var dupList = string.Join(", ", dupInBatch);
                    return Conflict(new { error = $"بيانات مكررة في نفس الملف: {dupList}", messageEn = $"Duplicate student IDs in the same file: {dupList}" });
                }

                // Pre-check: reject any student IDs that already exist in DB
                var newIdsHash = dto.Students.Where(s => !string.IsNullOrEmpty(s.StudentID)).Select(s => s.StudentID).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingIds = new HashSet<string>(await _context.Students
                    .Where(s => s.student_id != null && newIdsHash.Contains(s.student_id))
                    .Select(s => s.student_id!)
                    .ToListAsync(), StringComparer.OrdinalIgnoreCase);
                if (existingIds.Count > 0)
                {
                    var dupList = string.Join(", ", existingIds);
                    return Conflict(new { error = $"بعض الطلاب مسجلين مسبقاً: {dupList}", messageEn = $"Some students already registered: {dupList}" });
                }

                var bulkRequest = new BulkRequest
                {
                    FileName = dto.FileName,
                    RecordCount = dto.Students.Count,
                    ValidCount = dto.Students.Count,
                    ErrorCount = 0,
                    CreatedBy = actorId,
                    CreatedDate = DateTime.UtcNow,
                    Status = "pending"
                };
                _context.BulkRequests.Add(bulkRequest);
                await _context.SaveChangesAsync();

                bulkRequest.RequestNumber = $"BRQ-{bulkRequest.Id:D6}";

                var bulkStudents = dto.Students.Select(s => new BulkRequestStudent
                {
                    BulkRequestId = bulkRequest.Id,
                    StudentID = s.StudentID,
                    NationalID = s.NationalID,
                    FullNameArabic = s.FullNameArabic,
                    FullNameEnglish = s.FullNameEnglish,
                    Mobile = s.Mobile,
                    College = s.College,
                    Department = s.Department,
                    AcademicLevel = s.AcademicLevel,
                    Gender = s.Gender,
                    BuildingNumber = s.BuildingNumber,
                    ApartmentNumber = s.ApartmentNumber,
                    RoomNumber = s.RoomNumber,
                    IsValid = true
                }).ToList();

                _context.BulkRequestStudents.AddRange(bulkStudents);
                await _context.SaveChangesAsync();

                // Create individual Student records for each valid bulk student
                var students = dto.Students.Select(s => new Student
                {
                    student_id = s.StudentID,
                    national_id = s.NationalID,
                    full_name = s.FullNameArabic,
                    full_name_english = s.FullNameEnglish,
                    phone = s.Mobile,
                    college = s.College,
                    department = s.Department,
                    academic_level = s.AcademicLevel,
                    gender = GenderHelper.NormalizeSafely(s.Gender),
                    housing_building = s.BuildingNumber,
                    apartment_number = s.ApartmentNumber,
                    room_number = s.RoomNumber,
                    created_at = DateTime.UtcNow,
                    created_by = actorId,
                    status = "active",
                    student_status = "active"
                }).ToList();

                _context.Students.AddRange(students);
                await _context.SaveChangesAsync();

                // Create individual Request records for each student
                var defaultStatus = isHousingCreator ? "cyber_review" : "submitted";
                var requests = students.Select(st => new Request
                {
                    RequestType = "bulk_req",
                    StudentId = st.Id,
                    SubmittedBy = actorId,
                    SubmittedAt = DateTime.UtcNow,
                    Status = defaultStatus,
                    RequestedByRole = role,
                    BulkRequestId = bulkRequest.Id
                }).ToList();

                _context.Requests.AddRange(requests);
                await _context.SaveChangesAsync();

                // Create notification for each request
                var notifRoles = isHousingCreator ? new[] { "cyber" } : new[] { "admin", "supervisor", "cyber" };
                foreach (var req in requests)
                {
                    var reqNum = $"BRQ-{bulkRequest.Id:D6}-S{req.StudentId}";
                    var notifMsg = isHousingCreator
                        ? $"تم تقديم طلب جماعي جديد وإحالته للمراجعة الإلكترونية ({reqNum})"
                        : $"تم تقديم طلب جماعي جديد ({reqNum})";
                    foreach (var nr in notifRoles)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            request_id = req.Id,
                            channel = "in_app",
                            recipient_role = nr,
                            message = notifMsg,
                            status = "pending",
                            sent_at = DateTime.UtcNow
                        });
                    }

                    // Create audit log for each request
                    _context.AuditLogs.Add(new AuditLog
                    {
                        user_id = actorId,
                        action = "bulk_registration_created",
                        target_table = "Requests",
                        target_id = req.Id,
                        action_at = DateTime.UtcNow,
                        ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        user_agent = Request.Headers["User-Agent"].ToString()
                    });
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    id = bulkRequest.Id,
                    requestId = requests.First().Id,
                    requestNumber = bulkRequest.RequestNumber,
                    recordCount = bulkRequest.RecordCount
                });
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); }
                catch (Exception rollbackEx) { Console.Error.WriteLine("BulkRegistration/create rollback error: " + rollbackEx.ToString()); }
                var errorRef = Guid.NewGuid().ToString("N")[..12];
                var fullMsg = $"BulkRegistration/create error [Ref={errorRef}]: {ex}";
                Console.Error.WriteLine(fullMsg);
                try { System.IO.File.AppendAllText("bulk_create_errors.log", DateTime.UtcNow.ToString("o") + " " + fullMsg + "\n"); }
                catch { /* best effort file logging */ }
                var detail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { error = "حدث خطأ أثناء إنشاء الطلب الجماعي", detail, reference = errorRef });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BulkRequest>>> GetBulkRequests()
            => await _context.BulkRequests.OrderByDescending(b => b.CreatedDate).ToListAsync();

        [HttpGet("{id}")]
        public async Task<ActionResult<BulkRequest>> GetBulkRequest(int id)
        {
            var req = await _context.BulkRequests.Include(b => b.Students).FirstOrDefaultAsync(b => b.Id == id);
            if (req == null) return NotFound();
            return req;
        }
    }
}
