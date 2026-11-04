using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSignupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PhoneNumber 13 → 16 (E.164). Done by hand instead of AlterColumn: the live database lacks the
            // unique IX_Users_PhoneNumber index the model expects and already holds duplicate numbers, so
            // EF's drop-and-recreate of the index fails. The index is put back only where it can be.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_PhoneNumber' AND object_id = OBJECT_ID('dbo.Users'))
    DROP INDEX [IX_Users_PhoneNumber] ON [dbo].[Users];

ALTER TABLE [dbo].[Users] ALTER COLUMN [PhoneNumber] nvarchar(16) NULL;

IF NOT EXISTS (SELECT [PhoneNumber] FROM [dbo].[Users] WHERE [PhoneNumber] IS NOT NULL GROUP BY [PhoneNumber] HAVING COUNT(*) > 1)
    EXEC('CREATE UNIQUE INDEX [IX_Users_PhoneNumber] ON [dbo].[Users] ([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL');
");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataConsentAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarketingConsent",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsAcceptedIp",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsSignature",
                table: "Users",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                columns: new[] { "DataConsentAt", "DateOfBirth", "MarketingConsent", "TermsAcceptedAt", "TermsAcceptedIp", "TermsSignature", "TermsVersion" },
                values: new object[] { null, null, false, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataConsentAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MarketingConsent",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedIp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsSignature",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}
