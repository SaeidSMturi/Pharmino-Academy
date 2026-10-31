using Domain.Entities;
using Domain.Entities.Course;
using Domain.Entities.Email;
using Domain.Entities.Permission;
using Domain.Entities.Post;
using Domain.Entities.RecentActivity;
using Domain.Entities.Security;
using Domain.Entities.User;
using Domain.Entities.UserTraker;
using Domain.Entities.Seo;
using Domain.Entities.Exam;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;

namespace Infrastructure.Persistence
{
    public class WebContext : DbContext, IApplicationDbContext
    {
        public WebContext(DbContextOptions<WebContext> options) : base(options)
        {

        }

        #region User

        public DbSet<User> Users { get; set; }

        #endregion

        #region Post

        public DbSet<Post>? Posts { get; set; }

        #endregion

        #region Link

        public DbSet<Link> Links { get; set; }
        public DbSet<Visitor> Visitors { get; set; }

        #endregion

        #region RecentActivity

        public DbSet<RecentActivity> RecentActivities { get; set; }
        public DbSet<RecentActivityPriority> RecentActivityPriorities { get; set; }

        #endregion

        #region Email

        public DbSet<EmailAccount> Emails { get; set; }

        #endregion

        #region Permission & Role

        public DbSet<Permission> Permissions { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        #endregion

        #region Course

        public DbSet<Course> Courses { get; set; }
        public DbSet<CourseBundleItem> CourseBundleItems { get; set; }
        public DbSet<CourseEnrollment> CourseEnrollments { get; set; }
        public DbSet<CourseVideo> CourseVideos { get; set; }
        public DbSet<CoursePdf> CoursePdfs { get; set; }
        public DbSet<VideoProgress> VideoProgresses { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        #endregion

        #region Exam / Mock Tests

        public DbSet<Exam> Exams { get; set; }
        public DbSet<ExamQuestion> ExamQuestions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<ExamAttempt> ExamAttempts { get; set; }
        public DbSet<AttemptAnswer> AttemptAnswers { get; set; }

        #endregion

        #region Security

        public DbSet<SuspiciousActivity> SuspiciousActivities { get; set; }
        public DbSet<AccountLockHistory> AccountLockHistories { get; set; }
        public DbSet<UserActiveSession> UserActiveSessions { get; set; }
        public DbSet<UserLastKnownLocation> UserLastKnownLocations { get; set; }

        #endregion

        public DbSet<Setting> Settings { get; set; }
        public DbSet<Sms> Sms { get; set; }

        public DbSet<SeoRedirect> SeoRedirects { get; set; }
        public DbSet<SeoErrorLog> SeoErrorLogs { get; set; }
        public DbSet<SeoPageScan> SeoPageScans { get; set; }
        public DbSet<SeoImage> SeoImages { get; set; }
        public DbSet<SeoSitemapSetting> SeoSitemapSettings { get; set; }
        public DbSet<PageSeo> PageSeos { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttemptAnswer>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prices are in Omani Rials, which have 3 decimals (baisa).
            modelBuilder.Entity<Domain.Entities.Course.Course>().Property(c => c.Price).HasColumnType("decimal(18,3)");
            modelBuilder.Entity<Domain.Entities.Course.Course>().Property(c => c.DiscountPrice).HasColumnType("decimal(18,3)");
            modelBuilder.Entity<Domain.Entities.Course.CoursePdf>().Property(p => p.Price).HasColumnType("decimal(18,3)");
            modelBuilder.Entity<Domain.Entities.Course.CourseEnrollment>().Property(e => e.PaymentAmount).HasColumnType("decimal(18,3)");

            // Explicit column type (same as the existing columns, so no migration is needed) silences EF's truncation warning.
            modelBuilder.Entity<Exam>().Property(e => e.PassingScorePercent).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ExamQuestion>().Property(q => q.Points).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ExamAttempt>().Property(a => a.Score).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ExamAttempt>().Property(a => a.TotalPoints).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ExamAttempt>().Property(a => a.ScorePercent).HasColumnType("decimal(18,2)");

            // Users are soft-deleted with a query filter; hide their attempts the same way so the required
            // ExamAttempt → User navigation never points at a filtered-out user.
            modelBuilder.Entity<ExamAttempt>().HasQueryFilter(a => a.User.IsDelete == false);

            modelBuilder.Entity<PageSeo>()
                .HasIndex(p => p.PagePath)
                .IsUnique();
            modelBuilder.Entity<User>().HasQueryFilter(u => u.IsDelete == false);

            // یکتا بودن UserName (که ایمیل است)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.UserName)
                .IsUnique();

            // یکتا بودن PhoneNumber
            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();

            // یکتا بودن Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<CourseBundleItem>(entity =>
            {
                entity.HasKey(b => b.Id);

                entity.HasOne(b => b.Bundle)
                    .WithMany(c => c.BundleCourses)
                    .HasForeignKey(b => b.BundleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.Course)
                    .WithMany(c => c.IncludedInBundles)
                    .HasForeignKey(b => b.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(b => new { b.BundleId, b.CourseId })
                    .IsUnique();
            });

            // Force all DateTime properties to be stored/read as UTC
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                    }
                }
            }

            #region All User Data

            modelBuilder.Entity<User>().HasData(
                new User()
                {
                    UserId = 1,
                    FirstName = "Admin",
                    LastName = "",
                    ActiveCode = "811b78fab121434b8e20b2b263e6bd7e",
                    ActivePhoneNumberCode = "12345",
                    // username is email
                    Email = "admin@example.com",
                    UserName = "admin@example.com",
                    IsDelete = false,
                    IsEmailActive = true,
                    IsPhoneNumberActive = true,
                    LastChange = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    PhoneNumber = "09xxxxxxxxx",
                    RegisterDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    SecurityCode = "40c87d0a7d83413ab8e8c59229e8949e",
                    Password = "pmWkWSBCL51Bfkhn79xPuKBKHz//H6B+mY6G9/eieuM="
                });

            #endregion

            #region Settings Data

            modelBuilder.Entity<Setting>().HasData(
                // General Settings
                new Setting()
                {
                    Id = 1,
                    Title = "Site Name",
                    Category = "General",
                    Description = "Main name displayed across the site",
                    DataString = "Pharmacy Store",
                    Changeable = true,
                    DisplayOrder = 1,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 2,
                    Title = "Site Description",
                    Category = "General",
                    Description = "Meta description for search engines",
                    DataString = "Your trusted online pharmacy for quality medications and healthcare products",
                    Changeable = true,
                    DisplayOrder = 2,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 3,
                    Title = "Site Logo URL",
                    Category = "General",
                    Description = "URL path to site logo",
                    DataString = "/images/logo.png",
                    Changeable = true,
                    DisplayOrder = 3,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 4,
                    Title = "Support Email",
                    Category = "General",
                    Description = "Main support email address",
                    DataString = "support@pharmacy.com",
                    Changeable = true,
                    DisplayOrder = 4,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 5,
                    Title = "Support Phone",
                    Category = "General",
                    Description = "Customer support phone number",
                    DataString = "+98-21-1234-5678",
                    Changeable = true,
                    DisplayOrder = 5,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // Email Settings
                new Setting()
                {
                    Id = 6,
                    Title = "SMTP Server",
                    Category = "Email",
                    Description = "SMTP server address for sending emails",
                    DataString = "smtp.gmail.com",
                    Changeable = true,
                    DisplayOrder = 1,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 7,
                    Title = "SMTP Port",
                    Category = "Email",
                    Description = "SMTP port number",
                    DataInt = 587,
                    Changeable = true,
                    DisplayOrder = 2,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 8,
                    Title = "SMTP Username",
                    Category = "Email",
                    Description = "SMTP authentication username",
                    DataString = "noreply@pharmacy.com",
                    Changeable = true,
                    DisplayOrder = 3,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 9,
                    Title = "Enable Email Notifications",
                    Category = "Email",
                    Description = "Send email notifications to users",
                    DataBool = true,
                    Changeable = true,
                    DisplayOrder = 4,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // Security Settings
                new Setting()
                {
                    Id = 10,
                    Title = "Account Lockout Duration (Minutes)",
                    Category = "Security",
                    Description = "Minutes to lock account after failed attempts",
                    DataInt = 15,
                    Changeable = true,
                    DisplayOrder = 1,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 11,
                    Title = "Max Failed Login Attempts",
                    Category = "Security",
                    Description = "Maximum failed login attempts before lockout",
                    DataInt = 5,
                    Changeable = true,
                    DisplayOrder = 2,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 12,
                    Title = "Password Expiration Days",
                    Category = "Security",
                    Description = "Days before password expires (0 to disable)",
                    DataInt = 90,
                    Changeable = true,
                    DisplayOrder = 3,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 13,
                    Title = "Require Strong Password",
                    Category = "Security",
                    Description = "Enforce complex password requirements",
                    DataBool = true,
                    Changeable = true,
                    DisplayOrder = 4,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 14,
                    Title = "Two-Factor Authentication Required",
                    Category = "Security",
                    Description = "Require 2FA for admin users",
                    DataBool = false,
                    Changeable = true,
                    DisplayOrder = 5,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // Payment Settings
                new Setting()
                {
                    Id = 15,
                    Title = "Default Currency",
                    Category = "Payment",
                    Description = "Default currency for transactions",
                    DataString = "OMR",
                    Changeable = true,
                    DisplayOrder = 1,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 16,
                    Title = "Payment Gateway",
                    Category = "Payment",
                    Description = "Primary payment gateway provider",
                    DataString = "Stripe",
                    Changeable = true,
                    DisplayOrder = 2,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 17,
                    Title = "Transaction Fee Percentage",
                    Category = "Payment",
                    Description = "Percentage fee added to transactions",
                    DataDecimal = 2.5m,
                    Changeable = true,
                    DisplayOrder = 3,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 18,
                    Title = "Minimum Order Amount",
                    Category = "Payment",
                    Description = "Minimum amount for orders",
                    DataDecimal = 10.00m,
                    Changeable = true,
                    DisplayOrder = 4,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // Course Settings
                new Setting()
                {
                    Id = 19,
                    Title = "Course Enrollment Requires Approval",
                    Category = "Course",
                    Description = "Manual approval needed for course enrollment",
                    DataBool = false,
                    Changeable = true,
                    DisplayOrder = 1,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 20,
                    Title = "Course Completion Threshold (%)",
                    Category = "Course",
                    Description = "Percentage of course content required for completion",
                    DataInt = 80,
                    Changeable = true,
                    DisplayOrder = 2,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 21,
                    Title = "Course Certificate Auto-Generate",
                    Category = "Course",
                    Description = "Automatically generate certificate on completion",
                    DataBool = true,
                    Changeable = true,
                    DisplayOrder = 3,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 22,
                    Title = "Max Course Instructors",
                    Category = "Course",
                    Description = "Maximum number of instructors per course",
                    DataInt = 5,
                    Changeable = true,
                    DisplayOrder = 4,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // System Settings
                new Setting()
                {
                    Id = 23,
                    Title = "Enable Maintenance Mode",
                    Category = "System",
                    Description = "Put site in maintenance mode",
                    DataBool = false,
                    Changeable = true,
                    DisplayOrder = 1,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 24,
                    Title = "Session Timeout (Minutes)",
                    Category = "System",
                    Description = "User session expiration time in minutes",
                    DataInt = 30,
                    Changeable = true,
                    DisplayOrder = 2,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 25,
                    Title = "Log Activity",
                    Category = "System",
                    Description = "Enable activity logging",
                    DataBool = true,
                    Changeable = true,
                    DisplayOrder = 3,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 26,
                    Title = "Log Retention Days",
                    Category = "System",
                    Description = "Days to keep activity logs",
                    DataInt = 90,
                    Changeable = true,
                    DisplayOrder = 4,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Setting()
                {
                    Id = 27,
                    Title = "Max Upload File Size (MB)",
                    Category = "System",
                    Description = "Maximum file upload size in megabytes",
                    DataInt = 100,
                    Changeable = true,
                    DisplayOrder = 5,
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });

            #endregion

            #region RecentActivity

            modelBuilder.Entity<RecentActivityPriority>().HasData(
                new RecentActivityPriority()
                {
                    PriorityId = 1,
                    Title = "High"
                },
                new RecentActivityPriority()
                {
                    PriorityId = 2,
                    Title = "Medium"
                },
                new RecentActivityPriority()
                {
                    PriorityId = 3,
                    Title = "Low"
                });

            #endregion

            #region Permissions Data

            modelBuilder.Entity<Permission>().HasData(
                // Dashboard
                new Permission { PermissionId = 1, PermissionName = "Dashboard.View", DisplayName = "View Dashboard", Category = "Dashboard" },

                // Courses
                new Permission { PermissionId = 2, PermissionName = "Courses.View", DisplayName = "View Courses", Category = "Courses" },
                new Permission { PermissionId = 3, PermissionName = "Courses.Create", DisplayName = "Create Course", Category = "Courses" },
                new Permission { PermissionId = 4, PermissionName = "Courses.Edit", DisplayName = "Edit Course", Category = "Courses" },
                new Permission { PermissionId = 5, PermissionName = "Courses.Delete", DisplayName = "Delete Course", Category = "Courses" },
                new Permission { PermissionId = 6, PermissionName = "Courses.ManageEnrollments", DisplayName = "Manage Enrollments", Category = "Courses" },

                // Users
                new Permission { PermissionId = 7, PermissionName = "Users.View", DisplayName = "View Users", Category = "Users" },
                new Permission { PermissionId = 8, PermissionName = "Users.Create", DisplayName = "Create User", Category = "Users" },
                new Permission { PermissionId = 9, PermissionName = "Users.Edit", DisplayName = "Edit User", Category = "Users" },
                new Permission { PermissionId = 10, PermissionName = "Users.Delete", DisplayName = "Delete User", Category = "Users" },
                new Permission { PermissionId = 11, PermissionName = "Users.Ban", DisplayName = "Ban/Unban User", Category = "Users" },
                new Permission { PermissionId = 20, PermissionName = "Users.Students.View", DisplayName = "View Students", Category = "Users" },
                new Permission { PermissionId = 21, PermissionName = "Users.Admin.View", DisplayName = "View Admin Users", Category = "Users" },

                // Roles
                new Permission { PermissionId = 12, PermissionName = "Roles.View", DisplayName = "View Roles", Category = "Roles" },
                new Permission { PermissionId = 13, PermissionName = "Roles.Create", DisplayName = "Create Role", Category = "Roles" },
                new Permission { PermissionId = 14, PermissionName = "Roles.Edit", DisplayName = "Edit Role", Category = "Roles" },
                new Permission { PermissionId = 15, PermissionName = "Roles.Delete", DisplayName = "Delete Role", Category = "Roles" },
                new Permission { PermissionId = 16, PermissionName = "Roles.AssignPermissions", DisplayName = "Assign Permissions to Role", Category = "Roles" },
                new Permission { PermissionId = 17, PermissionName = "Users.AssignRoles", DisplayName = "Assign Roles to Users", Category = "Users" },

                // Payments
                new Permission { PermissionId = 18, PermissionName = "Payments.View", DisplayName = "View Payments", Category = "Payments" },
                new Permission { PermissionId = 19, PermissionName = "Payments.Manage", DisplayName = "Manage Payments", Category = "Payments" }
            );

            #endregion

            #region Roles Data

            modelBuilder.Entity<Role>().HasData(
                new Role
                {
                    RoleId = 1,
                    RoleName = "SuperAdmin",
                    DisplayName = "Super Administrator",
                    Description = "Full access to all features",
                    CreateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Role
                {
                    RoleId = 2,
                    RoleName = "CourseManager",
                    DisplayName = "Course Manager",
                    Description = "Manage courses and enrollments",
                    CreateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Role
                {
                    RoleId = 3,
                    RoleName = "Accountant",
                    DisplayName = "Accountant",
                    Description = "Manage payments only",
                    CreateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Role
                {
                    RoleId = 4,
                    RoleName = "IT",
                    DisplayName = "IT Administrator",
                    Description = "Technical and administrative access to manage courses, videos, users, and roles, excluding financial records",
                    CreateDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                }
            );

            #endregion

            #region RolePermissions Data

            // SuperAdmin - All permissions
            var superAdminPermissions = new List<RolePermission>();
            for (int i = 1; i <= 21; i++)
            {
                superAdminPermissions.Add(new RolePermission
                {
                    RolePermissionId = i,
                    RoleId = 1,
                    PermissionId = i
                });
            }

            // CourseManager - Course and Dashboard permissions
            var courseManagerPermissions = new List<RolePermission>
            {
                new RolePermission { RolePermissionId = 22, RoleId = 2, PermissionId = 1 }, // Dashboard.View
                new RolePermission { RolePermissionId = 23, RoleId = 2, PermissionId = 2 }, // Courses.View
                new RolePermission { RolePermissionId = 24, RoleId = 2, PermissionId = 3 }, // Courses.Create
                new RolePermission { RolePermissionId = 25, RoleId = 2, PermissionId = 4 }, // Courses.Edit
                new RolePermission { RolePermissionId = 26, RoleId = 2, PermissionId = 5 }, // Courses.Delete
                new RolePermission { RolePermissionId = 27, RoleId = 2, PermissionId = 6 }  // Courses.ManageEnrollments
            };

            // Accountant - Payment and Dashboard permissions
            var accountantPermissions = new List<RolePermission>
            {
                new RolePermission { RolePermissionId = 28, RoleId = 3, PermissionId = 1 },  // Dashboard.View
                new RolePermission { RolePermissionId = 29, RoleId = 3, PermissionId = 18 }, // Payments.View
                new RolePermission { RolePermissionId = 30, RoleId = 3, PermissionId = 19 }  // Payments.Manage
            };

            // IT - All permissions except Financial (Payments.View = 18, Payments.Manage = 19)
            var itPermissionIds = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 20, 21 };
            var itPermissions = new List<RolePermission>();
            int itRolePermissionId = 31;
            foreach (var permId in itPermissionIds)
            {
                itPermissions.Add(new RolePermission
                {
                    RolePermissionId = itRolePermissionId++,
                    RoleId = 4,
                    PermissionId = permId
                });
            }

            modelBuilder.Entity<RolePermission>().HasData(
                superAdminPermissions
                    .Concat(courseManagerPermissions)
                    .Concat(accountantPermissions)
                    .Concat(itPermissions)
            );

            #endregion

            #region UserRoles Data

            modelBuilder.Entity<UserRole>().HasData(
                new UserRole
                {
                    UserRoleId = 1,
                    UserId = 1,
                    RoleId = 1,
                    AssignedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            #endregion

            #region Session Security (SQL Server-backed)

            // یکتا بودن SessionId + جستجوی سریع بر اساس آن در هر درخواست (OnValidatePrincipal)
            // SessionId must be unique and is looked up on virtually every authenticated request.
            modelBuilder.Entity<UserActiveSession>()
                .HasIndex(s => s.SessionId)
                .IsUnique();

            // ایندکس ترکیبی برای شمارش/فهرست‌برداری سریع نشست‌های فعال یک کاربر و حذف قدیمی‌ترین آن‌ها
            // Composite index accelerates "count/list active sessions for user" and "evict oldest" queries.
            modelBuilder.Entity<UserActiveSession>()
                .HasIndex(s => new { s.UserId, s.LastActivityUtc });

            // ایندکس برای پاک‌سازی دوره‌ای نشست‌های منقضی‌شده توسط SessionCleanupBackgroundService
            // Speeds up the periodic cleanup service's scan for expired sessions.
            modelBuilder.Entity<UserActiveSession>()
                .HasIndex(s => s.ExpiresAtUtc);

            modelBuilder.Entity<UserActiveSession>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // هر کاربر فقط یک رکورد «آخرین موقعیت شناخته‌شده» دارد
            // Each user has exactly one "last known location" row (upserted on every login).
            modelBuilder.Entity<UserLastKnownLocation>()
                .HasIndex(l => l.UserId)
                .IsUnique();

            modelBuilder.Entity<UserLastKnownLocation>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region Payment Transactions

            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.HasKey(e => e.TransactionId);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Course)
                    .WithMany()
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            #endregion

            base.OnModelCreating(modelBuilder);
        }

    }
}
