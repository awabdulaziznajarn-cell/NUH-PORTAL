using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NUH_PORTAL.Migrations
{
    /// <inheritdoc />
    public partial class initalDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_id = table.Column<int>(type: "int", nullable: false),
                    channel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    recipient_role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
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
                    phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    college = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ad_username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    housing_building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    room_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    apartment_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    student_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestoredBy = table.Column<int>(type: "int", nullable: true),
                    RestoredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ad_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    full_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    job_title = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
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
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    submitted_by = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    action = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "IX_AuditChangeLogs_AuditLogId",
                table: "AuditChangeLogs",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_user_id",
                table: "AuditLogs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_BulkRequestStudents_BulkRequestId",
                table: "BulkRequestStudents",
                column: "BulkRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingTransfers_created_by",
                table: "HousingTransfers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_HousingTransfers_student_id",
                table: "HousingTransfers",
                column: "student_id");

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
                name: "IX_Requests_student_id",
                table: "Requests",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_SMSLogs_status",
                table: "SMSLogs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDeclarations_request_id",
                table: "StudentDeclarations",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_Students_national_id",
                table: "Students",
                column: "national_id",
                unique: true,
                filter: "[national_id] IS NOT NULL");

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
                name: "AuditChangeLogs");

            migrationBuilder.DropTable(
                name: "BulkRequestStudents");

            migrationBuilder.DropTable(
                name: "HousingTransfers");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OTPVerifications");

            migrationBuilder.DropTable(
                name: "RequestAttachments");

            migrationBuilder.DropTable(
                name: "SMSLogs");

            migrationBuilder.DropTable(
                name: "StudentDeclarations");

            migrationBuilder.DropTable(
                name: "StudentStatusAttachments");

            migrationBuilder.DropTable(
                name: "WorkflowHistory");

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
        }
    }
}
