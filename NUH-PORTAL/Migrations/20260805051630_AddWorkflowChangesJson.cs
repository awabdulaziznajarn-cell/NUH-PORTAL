using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NUH_PORTAL.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowChangesJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "changes_json",
                table: "WorkflowHistory",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "changes_json",
                table: "WorkflowHistory");
        }
    }
}
