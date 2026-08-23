namespace NUH_PORTAL.DTOs.Bulk
{
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
        // الدور: "0" = الأرضي، وبعدها 1..4. متخزّن كود مش نص عشان الفرز والمقارنة.
        public string? FloorNumber { get; set; }
        public string? ApartmentNumber { get; set; }
        public string? RoomNumber { get; set; }
    }

    public class BulkValidationResultDto
    {
        public int TotalRecords { get; set; }
        public int ValidRecords { get; set; }
        public int ErrorRecords { get; set; }
        public List<RowErrorDto>? Errors { get; set; }
        public List<PreviewDto>? Preview { get; set; }
        public string? FileName { get; set; }
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
        public string? FloorNumber { get; set; }
        public string? ApartmentNumber { get; set; }
        public string? RoomNumber { get; set; }
    }

    public class BulkCreateDto
    {
        public string FileName { get; set; } = string.Empty;
        public List<BulkStudentDto> Students { get; set; } = new();
    }

    public class BulkCreateResultDto
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string? RequestNumber { get; set; }
        public int RecordCount { get; set; }
    }

    // مرآة BulkRequest من غير الـ navigation
    public class BulkRequestDto
    {
        public int Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? FileName { get; set; }
        public int RecordCount { get; set; }
        public int ValidCount { get; set; }
        public int ErrorCount { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? Status { get; set; }
    }

    public class BulkRequestStudentDto
    {
        public int Id { get; set; }
        public int BulkRequestId { get; set; }
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
        public string? FloorNumber { get; set; }
        public string? ApartmentNumber { get; set; }
        public string? RoomNumber { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BulkRequestDetailsDto : BulkRequestDto
    {
        public List<BulkRequestStudentDto> Students { get; set; } = new();
    }
}
