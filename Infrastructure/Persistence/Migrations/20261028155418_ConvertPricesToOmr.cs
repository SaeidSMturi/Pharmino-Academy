using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPricesToOmr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "PaymentTransactions",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Courses",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPrice",
                table: "Courses",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "CoursePdfs",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PaymentAmount",
                table: "CourseEnrollments",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 15,
                column: "DataString",
                value: "OMR");

            // One-time conversion: prices were entered in US dollars; from now on they are Omani Rials.
            // 1 USD = 0.385 OMR (the rial's peg), rounded to the baisa. Enrollment payment amounts are
            // converted too so "paid" comparisons against course prices stay consistent.
            // PaymentTransactions are NOT converted: each one records its own currency.
            migrationBuilder.Sql("UPDATE [Courses] SET [Price] = ROUND([Price] * 0.385, 3), [DiscountPrice] = ROUND([DiscountPrice] * 0.385, 3);");
            migrationBuilder.Sql("UPDATE [CoursePdfs] SET [Price] = ROUND([Price] * 0.385, 3);");
            migrationBuilder.Sql("UPDATE [CourseEnrollments] SET [PaymentAmount] = ROUND([PaymentAmount] * 0.385, 3) WHERE [PaymentAmount] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to US dollars before the columns lose their third decimal.
            migrationBuilder.Sql("UPDATE [Courses] SET [Price] = ROUND([Price] / 0.385, 2), [DiscountPrice] = ROUND([DiscountPrice] / 0.385, 2);");
            migrationBuilder.Sql("UPDATE [CoursePdfs] SET [Price] = ROUND([Price] / 0.385, 2);");
            migrationBuilder.Sql("UPDATE [CourseEnrollments] SET [PaymentAmount] = ROUND([PaymentAmount] / 0.385, 2) WHERE [PaymentAmount] IS NOT NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "PaymentTransactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Courses",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPrice",
                table: "Courses",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "CoursePdfs",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PaymentAmount",
                table: "CourseEnrollments",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 15,
                column: "DataString",
                value: "USD");
        }
    }
}
