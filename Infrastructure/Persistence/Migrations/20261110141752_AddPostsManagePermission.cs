using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostsManagePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Permission for Admin → Content (articles) and the article editor's image upload.
            // Plain SQL (not HasData), like Emails.Manage: add it only if missing, then give it to
            // SuperAdmin (RoleId 1) and CourseManager (RoleId 2) when those roles exist and don't have it yet.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionName = N'Posts.Manage')
    INSERT INTO Permissions (PermissionName, DisplayName, Description, Category)
    VALUES (N'Posts.Manage', N'Manage Articles', N'Create, edit, publish and delete articles, and upload images into them', N'Content');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM Roles r CROSS JOIN Permissions p
WHERE p.PermissionName = N'Posts.Manage' AND r.RoleId IN (1, 2)
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE rp FROM RolePermissions rp JOIN Permissions p ON p.PermissionId = rp.PermissionId WHERE p.PermissionName = N'Posts.Manage';
DELETE FROM Permissions WHERE PermissionName = N'Posts.Manage';
");
        }
    }
}
