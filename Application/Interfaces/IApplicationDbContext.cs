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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<User> Users { get; set; }
        DbSet<Post>? Posts { get; set; }
        DbSet<Link> Links { get; set; }
        DbSet<Visitor> Visitors { get; set; }
        DbSet<RecentActivity> RecentActivities { get; set; }
        DbSet<RecentActivityPriority> RecentActivityPriorities { get; set; }
        DbSet<EmailAccount> Emails { get; set; }
        DbSet<Permission> Permissions { get; set; }
        DbSet<Role> Roles { get; set; }
        DbSet<RolePermission> RolePermissions { get; set; }
        DbSet<UserRole> UserRoles { get; set; }
        DbSet<Course> Courses { get; set; }
        DbSet<CourseBundleItem> CourseBundleItems { get; set; }
        DbSet<CourseEnrollment> CourseEnrollments { get; set; }
        DbSet<CourseVideo> CourseVideos { get; set; }
        DbSet<CoursePdf> CoursePdfs { get; set; }
        DbSet<VideoProgress> VideoProgresses { get; set; }
        DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        DbSet<SuspiciousActivity> SuspiciousActivities { get; set; }
        DbSet<AccountLockHistory> AccountLockHistories { get; set; }
        DbSet<UserActiveSession> UserActiveSessions { get; set; }
        DbSet<UserLastKnownLocation> UserLastKnownLocations { get; set; }
        DbSet<Setting> Settings { get; set; }
        DbSet<Sms> Sms { get; set; }

        DbSet<SeoRedirect> SeoRedirects { get; set; }
        DbSet<SeoErrorLog> SeoErrorLogs { get; set; }
        DbSet<SeoPageScan> SeoPageScans { get; set; }
        DbSet<SeoImage> SeoImages { get; set; }
        DbSet<SeoSitemapSetting> SeoSitemapSettings { get; set; }
        DbSet<PageSeo> PageSeos { get; set; }

        DatabaseFacade Database { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
