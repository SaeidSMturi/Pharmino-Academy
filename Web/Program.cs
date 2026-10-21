using Domain.Configuration;
using Application.Interfaces;
using Infrastructure.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.BackgroundServices;
using Application.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Runtime.InteropServices;
using Web.Middleware;
using Web.Services.Pdf;
using Web.Localization;
using WebMarkupMin.AspNetCoreLatest;
using WebMarkupMin.Core;
using WebMarkupMin.NUglify;

using Domain.Interfaces;
namespace Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

            builder.Services.AddControllersWithViews()
                .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
                .AddDataAnnotationsLocalization(options => {
                    options.DataAnnotationLocalizerProvider = (type, factory) =>
                        factory.Create(typeof(SharedResource));
                });

            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));

            // Configure Upload Limits Options
            builder.Services.Configure<UploadLimitsOptions>(
                builder.Configuration.GetSection(UploadLimitsOptions.SectionName));

            var uploadLimits = builder.Configuration
                .GetSection(UploadLimitsOptions.SectionName)
                .Get<UploadLimitsOptions>() ?? new UploadLimitsOptions();

            builder.Services.AddWebMarkupMin(options =>
                {
                    options.AllowMinificationInDevelopmentEnvironment = true;
                    options.AllowCompressionInDevelopmentEnvironment = true;
                })
                .AddHtmlMinification(options =>
                {
                    var settings = options.MinificationSettings;

                    // مدیریت فضاهای خالی با مد استاندارد و بدون خطا
                    settings.WhitespaceMinificationMode = WhitespaceMinificationMode.Medium;

                    settings.RemoveHtmlComments = true; // حذف کامنت‌های HTML
                    settings.MinifyEmbeddedCssCode = true; // فشرده‌سازی CSSهای داخل تگ <style>
                    settings.MinifyEmbeddedJsCode = true; // فشرده‌سازی JSهای داخل تگ <script>

                    // تنظیمات ایمن برای جلوگیری از به هم ریختن کلاس‌ها، آیکون‌ها و تگ‌های خاص
                    settings.RemoveOptionalEndTags = false;
                    settings.RemoveTagsWithoutContent = false;
                    settings.PreserveCase = true;

                    // استفاده از موتور قدرتمند NUglify برای مینیفای کدهای داخلی
                    options.CssMinifierFactory = new NUglifyCssMinifierFactory();
                    options.JsMinifierFactory = new NUglifyJsMinifierFactory();
                }).AddHttpCompression();

            // Configure FormOptions for multipart form data
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = uploadLimits.CourseVideoMaxBytes;
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartBoundaryLengthLimit = int.MaxValue;
                options.MultipartHeadersCountLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });

            // Configure Kestrel server limits
            builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = uploadLimits.CourseVideoMaxBytes;
            });

            builder.Services.AddDbContext<WebContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            // Register IApplicationDbContext mapping to WebContext
            builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<WebContext>());

            // فعال‌سازی سرویس Caching برای Rate Limiting
            builder.Services.AddMemoryCache(options =>
            {
                // محدودیت کل کش را مثلا روی 200 مگابایت تنظیم می‌کنیم
                options.SizeLimit = 1024 * 1024 * 1000;
            });
            // فعال‌سازی سیستم Data Protection برای ساخت توکن‌های امنیتی
            builder.Services.AddDataProtection();


            #region Session Security (SQL Server + EF Core-Backed Concurrent Session Management)

            // Configure Session Security Options (concurrent login limits, impossible-travel thresholds, cleanup interval, etc.)
            builder.Services.Configure<SessionSecurityOptions>(
                builder.Configuration.GetSection(SessionSecurityOptions.SectionName));

            // سرویس‌های مدیریت نشست، اثر انگشت دستگاه، GeoIP و تشخیص سفر غیرممکن
            // Core services powering concurrent-session management, device fingerprinting, GeoIP, and impossible-travel
            // detection. ISessionManagerService and IImpossibleTravelDetectionService are Scoped because they depend on
            // the scoped WebContext (EF Core) — all session state now lives in SQL Server, with no external cache/store.
            builder.Services.AddScoped<ISessionManagerService, SqlSessionManagerService>();
            builder.Services.AddSingleton<IDeviceFingerprintService, DeviceFingerprintService>();
            builder.Services.AddSingleton<IGeoIpService, MockGeoIpService>();
            builder.Services.AddSingleton<IUserAgentParserService, UserAgentParserService>();
            builder.Services.AddScoped<IImpossibleTravelDetectionService, ImpossibleTravelDetectionService>();

            // رویدادهای اعتبارسنجی کوکی باید از طریق DI Container Resolve شوند (نه new شدن مستقیم)
            // Cookie validation events must be DI-resolved (registered here, wired via options.EventsType below).
            builder.Services.AddScoped<SessionValidationCookieEvents>();

            // سرویس پس‌زمینه پاکسازی دوره‌ای نشست‌های منقضی و اسنپ‌شات‌های قدیمی موقعیت مکانی از SQL Server
            // Periodic background cleanup of expired sessions / stale location snapshots (replaces Redis TTL expiry).
            builder.Services.AddHostedService<SessionCleanupBackgroundService>();

            #endregion


            #region Authentication

            // Configure Password Policy Options
            builder.Services.Configure<PasswordPolicyOptions>(
                builder.Configuration.GetSection(PasswordPolicyOptions.SectionName));

            // 1. Setup Data Protection securely BEFORE adding Authentication
            string keyPath = Path.Combine(Directory.GetCurrentDirectory(), "Keys");

            // Ensure the directory exists so keys aren't saved in RAM ephemerally
            if (!Directory.Exists(keyPath))
                Directory.CreateDirectory(keyPath);

            builder.Services.AddDataProtection()
                .SetApplicationName("PharmacistsWeb") // Use a specific unique name
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
                .SetDefaultKeyLifetime(TimeSpan.FromDays(30));

            // 2. Add Authentication with correct defaults
            builder.Services.AddAuthentication(options =>
                {
                    // Set EVERYTHING to Cookie by default to avoid unexpected redirects
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromDays(30);
                    options.SlidingExpiration = true; // IMPORTANT: Renews the cookie if the user is active

                    // Resolve session-validation events from DI so it can use scoped services
                    // (ISessionManagerService, IDeviceFingerprintService) to enforce SQL Server-backed session + IP/Fingerprint binding
                    // on every request (see Web/Services/SessionValidationCookieEvents.cs).
                    options.EventsType = typeof(SessionValidationCookieEvents);
                });
            #endregion

            // Register DI Services
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
            builder.Services.AddScoped<IPasswordValidator, PasswordValidator>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IPermissionService, PermissionService>();
            builder.Services.AddScoped<ICourseService, CourseService>();
            builder.Services.Configure<Application.DTOs.Payment.PayPalSettings>(
                builder.Configuration.GetSection("PayPalSettings"));
            builder.Services.AddScoped<IPayPalService, PayPalService>();
            builder.Services.AddScoped<ISuspiciousActivityService, SuspiciousActivityService>();
            builder.Services.AddScoped<ISettingsService, SettingsService>();
            builder.Services.AddScoped<ISeoService, SeoService>();
            builder.Services.AddSingleton<IVideoTokenService, VideoTokenService>();
            builder.Services.AddSingleton<IVideoSessionManager, VideoSessionManager>();

            bool useS3 = builder.Configuration.GetValue<bool>("StorageSettings:UseS3Storage");

            if (useS3)
            {
                builder.Services.AddSingleton<IStorageService, S3StorageService>();
            }
            else
            {
                builder.Services.AddSingleton<IStorageService, LocalStorageService>();
            }

            // 🔒 Secure PDF delivery: files stored outside wwwroot, pages rendered server-side with a burned-in forensic watermark
            builder.Services.AddSingleton<IPdfFileLocator, PdfFileLocator>();
            builder.Services.AddSingleton<ISecurePdfRenderer, SecurePdfRenderer>();

            // ثبت سرویس مدیریت لاگ‌ها
            builder.Services.AddScoped<ILogManagerService, LogManagerService>();

            // ثبت سرویس پس‌زمینه برای حذف خودکار ۳۰ روزه لاگ‌ها
            builder.Services.AddHostedService<AutoDeleteLogsBackgroundService>();

            // Advanced security services
            builder.Services.AddSingleton<IVideoSegmentationService, VideoSegmentationService>();
            builder.Services.AddSingleton<IOneTimeUrlService, OneTimeUrlService>();
            builder.Services.AddSingleton<IIpBindingService, IpBindingService>();

            // Advanced anti-download services
            builder.Services.AddSingleton<IBrowserFingerprintService, BrowserFingerprintService>();
            builder.Services.AddSingleton<IAdvancedBehaviorAnalyzer, AdvancedBehaviorAnalyzer>();
            builder.Services.AddSingleton<IHlsProgressService, HlsProgressService>();

            // 🎬 HLS Processing Service — OS-aware
            //   Windows (dev): IndependentHlsProcessingService — Xabe.FFmpeg دانلود خودکار ffmpeg.exe
            //   Linux/Ubuntu:  HlsProcessingService — فراخوانی مستقیم /usr/bin/ffmpeg
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                builder.Services.AddSingleton<IHlsProcessingService, IndependentHlsProcessingService>();
            else
                builder.Services.AddSingleton<IHlsProcessingService, HlsProcessingService>();

            // Background service for auto-unlocking accounts
            builder.Services.AddHostedService<AutoUnlockAccountsService>();

            // Add Session
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            app.UseSerilogRequestLogging();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
            app.UseHttpsRedirection();
            // 🔒 Course PDFs must never be served as static files; they only go out as watermarked page images.
            //    (Covers any old files still sitting in wwwroot/uploads/courses/{id}/pdfs before they are auto-migrated.)
            app.Use(async (context, next) =>
            {
                var requestPath = context.Request.Path.Value ?? string.Empty;
                if (requestPath.StartsWith("/uploads/courses/", StringComparison.OrdinalIgnoreCase) &&
                    (requestPath.Contains("/pdfs/", StringComparison.OrdinalIgnoreCase) ||
                     requestPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next();
            });
            app.UseStaticFiles();
            app.UseWebMarkupMin();

            var supportedCultures = new[] { "en", "ar" };
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture("en")
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            // Use our custom advanced detection mechanism
            localizationOptions.RequestCultureProviders.Clear();
            localizationOptions.RequestCultureProviders.Insert(0, new SmartRequestCultureProvider());

            app.UseRouting();

            app.UseSeoRedirectsAndMonitoring();

            app.UseSession();

            // استفاده از Anti-Download Manager Middleware
            app.UseAntiDownloadManager();

            app.UseAuthentication();

            app.UseRequestLocalization(localizationOptions);

            // بررسی قفل شدن اکانت (بعد از Authentication)
            app.UseAccountLockCheck();

            app.UseAuthorization();

            // Area routing
            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapControllers();

            app.Run();
        }
    }
}
