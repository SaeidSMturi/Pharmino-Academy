using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mailboxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mailboxes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mailboxes_Address",
                table: "Mailboxes",
                column: "Address",
                unique: true);

            // Permission for Admin → Emails. Plain SQL (not HasData) so it can't collide with permissions or
            // role assignments already edited on the live database: add it only if missing, then give it to
            // SuperAdmin (RoleId 1) and IT (RoleId 4) when those roles exist and don't have it yet.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionName = N'Emails.Manage')
    INSERT INTO Permissions (PermissionName, DisplayName, Description, Category)
    VALUES (N'Emails.Manage', N'Manage Email Accounts', N'Create mailboxes on the mail server, change their passwords, quotas, aliases and forwarding, suspend or delete them', N'Emails');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM Roles r CROSS JOIN Permissions p
WHERE p.PermissionName = N'Emails.Manage' AND r.RoleId IN (1, 4)
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE rp FROM RolePermissions rp JOIN Permissions p ON p.PermissionId = rp.PermissionId WHERE p.PermissionName = N'Emails.Manage';
DELETE FROM Permissions WHERE PermissionName = N'Emails.Manage';
");

            migrationBuilder.DropTable(
                name: "Mailboxes");
        }
    }
}
