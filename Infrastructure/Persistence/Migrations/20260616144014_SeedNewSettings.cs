using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedNewSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DataString", "Description", "DisplayOrder", "Title", "UpdatedAt" },
                values: new object[] { "Pharmacy Store", "Main name displayed across the site", 1, "Site Name", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5216) });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DataString", "Description", "DisplayOrder", "Title", "UpdatedAt" },
                values: new object[] { "Your trusted online pharmacy for quality medications and healthcare products", "Meta description for search engines", 2, "Site Description", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5405) });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "Category", "Changeable", "DataBool", "DataDecimal", "DataInt", "DataJson", "DataString", "Description", "DisplayOrder", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 3, "General", true, null, null, null, null, "/images/logo.png", "URL path to site logo", 3, "Site Logo URL", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5408) },
                    { 4, "General", true, null, null, null, null, "support@pharmacy.com", "Main support email address", 4, "Support Email", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5410) },
                    { 5, "General", true, null, null, null, null, "+98-21-1234-5678", "Customer support phone number", 5, "Support Phone", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5413) },
                    { 6, "Email", true, null, null, null, null, "smtp.gmail.com", "SMTP server address for sending emails", 1, "SMTP Server", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5416) },
                    { 7, "Email", true, null, null, 587, null, null, "SMTP port number", 2, "SMTP Port", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5585) },
                    { 8, "Email", true, null, null, null, null, "noreply@pharmacy.com", "SMTP authentication username", 3, "SMTP Username", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5588) },
                    { 9, "Email", true, true, null, null, null, null, "Send email notifications to users", 4, "Enable Email Notifications", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5745) },
                    { 10, "Security", true, null, null, 15, null, null, "Minutes to lock account after failed attempts", 1, "Account Lockout Duration (Minutes)", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5749) },
                    { 11, "Security", true, null, null, 5, null, null, "Maximum failed login attempts before lockout", 2, "Max Failed Login Attempts", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5752) },
                    { 12, "Security", true, null, null, 90, null, null, "Days before password expires (0 to disable)", 3, "Password Expiration Days", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5755) },
                    { 13, "Security", true, true, null, null, null, null, "Enforce complex password requirements", 4, "Require Strong Password", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5759) },
                    { 14, "Security", true, false, null, null, null, null, "Require 2FA for admin users", 5, "Two-Factor Authentication Required", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5792) },
                    { 15, "Payment", true, null, null, null, null, "USD", "Default currency for transactions", 1, "Default Currency", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5796) },
                    { 16, "Payment", true, null, null, null, null, "Stripe", "Primary payment gateway provider", 2, "Payment Gateway", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(5799) },
                    { 17, "Payment", true, null, 2.5m, null, null, null, "Percentage fee added to transactions", 3, "Transaction Fee Percentage", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6490) },
                    { 18, "Payment", true, null, 10.00m, null, null, null, "Minimum amount for orders", 4, "Minimum Order Amount", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6494) },
                    { 19, "Course", true, false, null, null, null, null, "Manual approval needed for course enrollment", 1, "Course Enrollment Requires Approval", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6498) },
                    { 20, "Course", true, null, null, 80, null, null, "Percentage of course content required for completion", 2, "Course Completion Threshold (%)", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6501) },
                    { 21, "Course", true, true, null, null, null, null, "Automatically generate certificate on completion", 3, "Course Certificate Auto-Generate", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6504) },
                    { 22, "Course", true, null, null, 5, null, null, "Maximum number of instructors per course", 4, "Max Course Instructors", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6507) },
                    { 23, "System", true, false, null, null, null, null, "Put site in maintenance mode", 1, "Enable Maintenance Mode", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6510) },
                    { 24, "System", true, null, null, 30, null, null, "User session expiration time in minutes", 2, "Session Timeout (Minutes)", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6512) },
                    { 25, "System", true, true, null, null, null, null, "Enable activity logging", 3, "Log Activity", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6515) },
                    { 26, "System", true, null, null, 90, null, null, "Days to keep activity logs", 4, "Log Retention Days", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6518) },
                    { 27, "System", true, null, null, 100, null, null, "Maximum file upload size in megabytes", 5, "Max Upload File Size (MB)", new DateTime(2026, 6, 16, 14, 40, 13, 654, DateTimeKind.Utc).AddTicks(6542) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DataString", "Description", "DisplayOrder", "Title", "UpdatedAt" },
                values: new object[] { "فروشگاه", null, 0, "نام سایت", new DateTime(2026, 6, 16, 14, 39, 39, 114, DateTimeKind.Utc).AddTicks(5997) });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DataString", "Description", "DisplayOrder", "Title", "UpdatedAt" },
                values: new object[] { "این سایت برای مدریت و فروش بهتر شما اینجاست", "شما میتوانید در توضیحات متا در صفحه اصلی ببنید", 0, "توضیحات سایت", new DateTime(2026, 6, 16, 14, 39, 39, 114, DateTimeKind.Utc).AddTicks(6964) });
        }
    }
}
