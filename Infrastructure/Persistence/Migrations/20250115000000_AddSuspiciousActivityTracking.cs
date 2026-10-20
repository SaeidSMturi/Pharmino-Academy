using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspiciousActivityTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // اضافه کردن فیلدهای جدید به جدول Users
            migrationBuilder.AddColumn<bool>(
                name: "IsLockedForSuspiciousActivity",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuspiciousActivityCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ایجاد جدول SuspiciousActivities
            migrationBuilder.CreateTable(
                name: "SuspiciousActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SuspicionScore = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CourseId = table.Column<int>(type: "int", nullable: true),
                    VideoId = table.Column<int>(type: "int", nullable: true),
                    VideoFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdditionalData = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResultedInBlock = table.Column<bool>(type: "bit", nullable: false),
                    BlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedByAdmin = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuspiciousActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuspiciousActivities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SuspiciousActivities_Users_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            // ایجاد جدول AccountLockHistories
            migrationBuilder.CreateTable(
                name: "AccountLockHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SuspiciousActivityCount = table.Column<int>(type: "int", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedByAdminId = table.Column<int>(type: "int", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLockHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountLockHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountLockHistories_Users_PerformedByAdminId",
                        column: x => x.PerformedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            // ایجاد Index ها برای بهبود Performance
            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_UserId",
                table: "SuspiciousActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_ReviewedByAdminId",
                table: "SuspiciousActivities",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_DetectedAt",
                table: "SuspiciousActivities",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_UserId_DetectedAt",
                table: "SuspiciousActivities",
                columns: new[] { "UserId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockHistories_UserId",
                table: "AccountLockHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockHistories_PerformedByAdminId",
                table: "AccountLockHistories",
                column: "PerformedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockHistories_ActionDate",
                table: "AccountLockHistories",
                column: "ActionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsLockedForSuspiciousActivity",
                table: "Users",
                column: "IsLockedForSuspiciousActivity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuspiciousActivities");

            migrationBuilder.DropTable(
                name: "AccountLockHistories");

            migrationBuilder.DropIndex(
                name: "IX_Users_IsLockedForSuspiciousActivity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsLockedForSuspiciousActivity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspiciousActivityCount",
                table: "Users");
        }
    }
}
