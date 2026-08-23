using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NUH_PORTAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPledgeAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "terms_hash",
                table: "StudentDeclarations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terms_text",
                table: "StudentDeclarations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "typed_confirmation",
                table: "StudentDeclarations",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "terms_hash",
                table: "StudentDeclarations");

            migrationBuilder.DropColumn(
                name: "terms_text",
                table: "StudentDeclarations");

            migrationBuilder.DropColumn(
                name: "typed_confirmation",
                table: "StudentDeclarations");
        }
    }
}
