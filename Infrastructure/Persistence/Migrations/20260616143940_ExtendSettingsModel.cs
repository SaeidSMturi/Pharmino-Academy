using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendSettingsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DataDecimal",
                table: "Settings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataJson",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Settings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Settings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Category", "DataDecimal", "DataJson", "DisplayOrder", "UpdatedAt" },
                values: new object[] { "General", null, null, 0, new DateTime(2026, 6, 16, 14, 39, 39, 114, DateTimeKind.Utc).AddTicks(5997) });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Category", "DataDecimal", "DataJson", "DisplayOrder", "UpdatedAt" },
                values: new object[] { "General", null, null, 0, new DateTime(2026, 6, 16, 14, 39, 39, 114, DateTimeKind.Utc).AddTicks(6964) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DataDecimal",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DataJson",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Settings");
        }
    }
}
