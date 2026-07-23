using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NUH_PORTAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ArName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ArName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BulkRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    ValidCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Colleges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ArName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colleges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OTPVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    mobile = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    otp_hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    verified_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    ip_address = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OTPVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SMSLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    mobile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    sent_date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SMSLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EnText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    full_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    mobile = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    job_title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BulkRequestStudents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BulkRequestId = table.Column<int>(type: "int", nullable: false),
                    StudentID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NationalID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullNameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullNameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    College = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcademicLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApartmentNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkRequestStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulkRequestStudents_BulkRequests_BulkRequestId",
                        column: x => x.BulkRequestId,
                        principalTable: "BulkRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ArName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CollegeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Colleges_CollegeId",
                        column: x => x.CollegeId,
                        principalTable: "Colleges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ADConfiguration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    config_key = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    config_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    updated_by = table.Column<int>(type: "int", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADConfiguration", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ADConfiguration_Users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    action = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    target_table = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    target_id = table.Column<int>(type: "int", nullable: false),
                    action_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ip_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    exception_type = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    stack_trace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    request_path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    request_method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    status_code = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorLogs_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SignInLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    event_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    success = table.Column<bool>(type: "bit", nullable: false),
                    detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignInLogs_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    student_id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    full_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    national_id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    full_name_english = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    academic_level = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    phone = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    college = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    housing_building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    room_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    apartment_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    student_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CollegeId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    BuildingId = table.Column<int>(type: "int", nullable: true),
                    AcademicLevelId = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestoredBy = table.Column<int>(type: "int", nullable: true),
                    RestoredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ad_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ad_last_sync_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ad_extension_phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_extension_building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_extension_room = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_extension_apartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_extension_college = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_extension_department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_extension_academic_level = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_AcademicLevels_AcademicLevelId",
                        column: x => x.AcademicLevelId,
                        principalTable: "AcademicLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Colleges_CollegeId",
                        column: x => x.CollegeId,
                        principalTable: "Colleges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditLogId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditChangeLogs_AuditLogs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "AuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountLifecycleLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    performed_by = table.Column<int>(type: "int", nullable: false),
                    performed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLifecycleLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountLifecycleLogs_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountLifecycleLogs_Users_performed_by",
                        column: x => x.performed_by,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HousingTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    student_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    old_building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    old_apartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    old_room = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    new_building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    new_apartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    new_room = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    custom_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    attachment_path = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    original_file_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousingTransfers_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HousingTransfers_Users_created_by",
                        column: x => x.created_by,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_type = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    submitted_by = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reviewed_by = table.Column<int>(type: "int", nullable: true),
                    requested_by_role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    housing_reviewed_by = table.Column<int>(type: "int", nullable: true),
                    housing_reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    housing_notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    cyber_reviewed_by = table.Column<int>(type: "int", nullable: true),
                    cyber_reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cyber_notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ready_for_provisioning_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ready_for_provisioning_by = table.Column<int>(type: "int", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_by = table.Column<int>(type: "int", nullable: true),
                    bulk_request_id = table.Column<int>(type: "int", nullable: true),
                    request_number = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    registration_data = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requests_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentStatusActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    StudentNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PendingADAction = table.Column<bool>(type: "bit", nullable: false),
                    ADActionCompleted = table.Column<bool>(type: "bit", nullable: true),
                    ADActionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentStatusActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentStatusActions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentStatusActions_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_id = table.Column<int>(type: "int", nullable: false),
                    channel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    recipient_role = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Requests_request_id",
                        column: x => x.request_id,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_id = table.Column<int>(type: "int", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    original_file_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    content_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    document_type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    uploaded_by = table.Column<int>(type: "int", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestAttachments_Requests_request_id",
                        column: x => x.request_id,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestAttachments_Users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_id = table.Column<int>(type: "int", nullable: false),
                    declaration_accepted = table.Column<bool>(type: "bit", nullable: false),
                    policy_accepted = table.Column<bool>(type: "bit", nullable: false),
                    policy_version = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    accepted_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ip_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentDeclarations_Requests_request_id",
                        column: x => x.request_id,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_id = table.Column<int>(type: "int", nullable: false),
                    from_stage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    to_stage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    action_by = table.Column<int>(type: "int", nullable: false),
                    action_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowHistory_Requests_request_id",
                        column: x => x.request_id,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowHistory_Users_action_by",
                        column: x => x.action_by,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentStatusAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    student_status_action_id = table.Column<int>(type: "int", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    original_file_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    content_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by = table.Column<int>(type: "int", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentStatusAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentStatusAttachments_StudentStatusActions_student_status_action_id",
                        column: x => x.student_status_action_id,
                        principalTable: "StudentStatusActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentStatusAttachments_Users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicLevels_Code",
                table: "AcademicLevels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountLifecycleLogs_performed_by",
                table: "AccountLifecycleLogs",
                column: "performed_by");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLifecycleLogs_student_id",
                table: "AccountLifecycleLogs",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_ADConfiguration_config_key",
                table: "ADConfiguration",
                column: "config_key",
                unique: true,
                filter: "[config_key] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ADConfiguration_updated_by",
                table: "ADConfiguration",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditChangeLogs_AuditLogId",
                table: "AuditChangeLogs",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_action",
                table: "AuditLogs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_action_at",
                table: "AuditLogs",
                column: "action_at");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_user_id",
                table: "AuditLogs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_Code",
                table: "Buildings",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_gender",
                table: "Buildings",
                column: "Gender");

            migrationBuilder.CreateIndex(
                name: "IX_BulkRequestStudents_BulkRequestId",
                table: "BulkRequestStudents",
                column: "BulkRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Colleges_Code",
                table: "Colleges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CollegeId",
                table: "Departments",
                column: "CollegeId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_occurred_at",
                table: "ErrorLogs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_user_id",
                table: "ErrorLogs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_HousingTransfers_created_by",
                table: "HousingTransfers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_HousingTransfers_student_id",
                table: "HousingTransfers",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_request_id",
                table: "Notifications",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_role_status",
                table: "Notifications",
                columns: new[] { "recipient_role", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_OTPVerifications_expires",
                table: "OTPVerifications",
                column: "expires_at",
                filter: "[verified_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTPVerifications_mobile_verified",
                table: "OTPVerifications",
                columns: new[] { "mobile", "verified_at" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestAttachments_request_deleted",
                table: "RequestAttachments",
                columns: new[] { "request_id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestAttachments_uploaded_by",
                table: "RequestAttachments",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_request_number",
                table: "Requests",
                column: "request_number",
                unique: true,
                filter: "[request_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_status",
                table: "Requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_student_id",
                table: "Requests",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_type_status",
                table: "Requests",
                columns: new[] { "request_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_SignInLogs_event_type",
                table: "SignInLogs",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_SignInLogs_occurred_at",
                table: "SignInLogs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_SignInLogs_user_id",
                table: "SignInLogs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_SMSLogs_status",
                table: "SMSLogs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDeclarations_request_id",
                table: "StudentDeclarations",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_Students_AcademicLevelId",
                table: "Students",
                column: "AcademicLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_BuildingId",
                table: "Students",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_CollegeId",
                table: "Students",
                column: "CollegeId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_deleted_status",
                table: "Students",
                columns: new[] { "IsDeleted", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_DepartmentId",
                table: "Students",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_national_id",
                table: "Students",
                column: "national_id",
                unique: true,
                filter: "[national_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_phone",
                table: "Students",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "IX_Students_student_id",
                table: "Students",
                column: "student_id",
                unique: true,
                filter: "[student_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentStatusActions_CreatedBy",
                table: "StudentStatusActions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentStatusActions_StudentId",
                table: "StudentStatusActions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentStatusAttachments_student_status_action_id",
                table: "StudentStatusAttachments",
                column: "student_status_action_id");

            migrationBuilder.CreateIndex(
                name: "IX_StudentStatusAttachments_uploaded_by",
                table: "StudentStatusAttachments",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_mobile",
                table: "Users",
                column: "mobile",
                unique: true,
                filter: "[mobile] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowHistory_action_by",
                table: "WorkflowHistory",
                column: "action_by");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowHistory_request_date",
                table: "WorkflowHistory",
                columns: new[] { "request_id", "action_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountLifecycleLogs");

            migrationBuilder.DropTable(
                name: "ADConfiguration");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditChangeLogs");

            migrationBuilder.DropTable(
                name: "BulkRequestStudents");

            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DropTable(
                name: "HousingTransfers");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OTPVerifications");

            migrationBuilder.DropTable(
                name: "RequestAttachments");

            migrationBuilder.DropTable(
                name: "SignInLogs");

            migrationBuilder.DropTable(
                name: "SMSLogs");

            migrationBuilder.DropTable(
                name: "StudentDeclarations");

            migrationBuilder.DropTable(
                name: "StudentStatusAttachments");

            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropTable(
                name: "WorkflowHistory");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BulkRequests");

            migrationBuilder.DropTable(
                name: "StudentStatusActions");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "AcademicLevels");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Colleges");
        }
    }
}
