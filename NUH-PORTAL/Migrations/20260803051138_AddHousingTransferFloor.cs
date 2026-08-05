using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NUH_PORTAL.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingTransferFloor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewFloor",
                table: "HousingTransfers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldFloor",
                table: "HousingTransfers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewFloor",
                table: "HousingTransfers");

            migrationBuilder.DropColumn(
                name: "OldFloor",
                table: "HousingTransfers");
        }
    }
}
