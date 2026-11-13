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
            // Real GeoIP from a local MaxMind GeoLite2-City database when it's installed; otherwise every location
            // is "unknown" and location-based checks are skipped (never guessed).
            builder.Services.AddSingleton<IGeoIpService>(sp =>
            {
                var configured = builder.Configuration["GeoIp:DatabasePath"] ?? "App_Data/GeoLite2-City.mmdb";
                var path = Path.IsPathRooted(configured) ? configured : Path.Combine(builder.Environment.ContentRootPath, configured);
                var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("GeoIp");
                if (File.Exists(path))
                {
                    log.LogInformation("GeoIP: using MaxMind database at {Path}.", path);
                    return new MaxMindGeoIpService(path, sp.GetRequiredService<ILogger<MaxMindGeoIpService>>());
                }
                log.LogWarning("GeoIP: no database at {Path}. Country defaults, language-by-country and impossible-travel checks are off until it is installed.", path);
                return new NullGeoIpService();
            });
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
            // Admin → Emails: mailboxes on the HestiaCP mail server ("Hestia" section of appsettings).
            // Locally, with no Hestia connection set, an in-memory demo server stands in so the pages can be tried out.
            var hestiaOptions = builder.Configuration.GetSection("Hestia").Get<Web.Services.Mail.HestiaOptions>() ?? new Web.Services.Mail.HestiaOptions();
            builder.Services.AddSingleton(hestiaOptions);
            builder.Services.AddHttpClient(Web.Services.Mail.HestiaMailServer.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(20))
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var handler = new SocketsHttpHandler();
                    // Optional: reach the panel at a fixed address (e.g. 127.0.0.1 on the same server); the certificate is still checked by name.
                    if (System.Net.IPAddress.TryParse(hestiaOptions.ConnectAddress, out var address))
                    {
                        handler.ConnectCallback = async (ctx, ct) =>
                        {
                            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
                            try { await socket.ConnectAsync(address, ctx.DnsEndPoint.Port, ct); return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true); }
                            catch { socket.Dispose(); throw; }
                        };
                    }
                    return handler;
                });
            if (builder.Environment.IsDevelopment() && !hestiaOptions.Configured)
                builder.Services.AddSingleton<Web.Services.Mail.IMailServerAdmin, Web.Services.Mail.DemoMailServer>();
            else
                builder.Services.AddScoped<Web.Services.Mail.IMailServerAdmin, Web.Services.Mail.HestiaMailServer>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            // Email codes and verification tokens for the one-page sign-up (in memory until the account exists).
            builder.Services.AddSingleton<Web.Services.Signup.SignupVerificationService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IPermissionService, PermissionService>();
            builder.Services.AddScoped<ICourseService, CourseService>();
            builder.Services.Configure<Application.DTOs.Payment.PayPalSettings>(
                builder.Configuration.GetSection("PayPalSettings"));
            builder.Services.AddScoped<IPayPalService, PayPalService>();
            builder.Services.Configure<Application.DTOs.Payment.LemonSqueezySettings>(
                builder.Configuration.GetSection("LemonSqueezy"));
            builder.Services.AddScoped<ILemonSqueezyService, LemonSqueezyService>();
            builder.Services.AddScoped<ISuspiciousActivityService, SuspiciousActivityService>();
            builder.Services.AddScoped<ISettingsService, SettingsService>();
            builder.Services.AddScoped<ISeoService, SeoService>();
            builder.Services.AddSingleton<Web.Seo.SeoErrorFilter>();
            builder.Services.AddSingleton<Web.Services.CourseCoverCache>();
            builder.Services.AddScoped<Web.Services.StudentEnrollments>();
            // Article editor (Admin → Content): image compression and server-side HTML clean-up.
            // Image module (SkiaSharp, MIT): uploads, cover resizing and PDF watermarks all go through it.
            builder.Services.AddSingleton<Web.Services.Imaging.IImageService, Web.Services.Imaging.SkiaImageService>();
            builder.Services.AddSingleton<Web.Services.Content.ArticleImageProcessor>();
            builder.Services.AddSingleton<Web.Services.Content.ArticleHtmlSanitizer>();
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
            builder.Services.AddScoped<IPdfExamParserService, PdfExamParserService>();

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

            // Payments: say loudly at startup if Live/Test mode is missing keys (checkout is refused until they are set).
            {
                var paySettings = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Application.DTOs.Payment.LemonSqueezySettings>>().Value;
                var missing = paySettings.MissingForRealPayments();
                if (missing.Count > 0)
                    app.Logger.LogCritical("Lemon Squeezy is in {Mode} mode but these settings are empty: {Missing}. Students cannot pay until they are set (env vars LemonSqueezy__<Name>).",
                        paySettings.Mode, string.Join(", ", missing));
                else
                    app.Logger.LogInformation("Lemon Squeezy payments mode: {Mode}.", paySettings.Mode);
            }

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
            // 🌐 One host only: https://www.example.com/x -> https://example.com/x (301), so Google never sees two copies of the site.
            app.Use(async (context, next) =>
            {
                var host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? context.Request.Host.Host;
                if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Contains('.') && !host.Equals("www.localhost", StringComparison.OrdinalIgnoreCase))
                {
                    var bare = host[4..];
                    var port = context.Request.Host.Port.HasValue && context.Request.Host.Port != 80 && context.Request.Host.Port != 443 ? $":{context.Request.Host.Port}" : "";
                    context.Response.Redirect($"{context.Request.Scheme}://{bare}{port}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}", permanent: true);
                    return;
                }
                await next();
            });
            app.UseHttpsRedirection();

            // 🔄 Terminology Route Redirect: /User -> /Students
            app.Use(async (context, next) =>
            {
                var requestPath = context.Request.Path.Value ?? string.Empty;
                if (requestPath.Equals("/User", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/Students" + context.Request.QueryString, permanent: false);
                    return;
                }
                if (requestPath.StartsWith("/User/", StringComparison.OrdinalIgnoreCase))
                {
                    var newPath = "/Students" + requestPath.Substring(5) + context.Request.QueryString;
                    context.Response.Redirect(newPath, permanent: false);
                    return;
                }
                await next();
            });

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

            // 🖼️ Course Covers Persistence & Cache Fallback
            // Restores missing course covers automatically from S3/Spaces if the app folder is deleted during redeployment
            app.Use(async (context, next) =>
            {
                var requestPath = context.Request.Path.Value ?? string.Empty;
                if (requestPath.StartsWith("/uploads/courses/", StringComparison.OrdinalIgnoreCase) &&
                    !requestPath.Contains("/pdfs/", StringComparison.OrdinalIgnoreCase) &&
                    !requestPath.Contains("/videos/", StringComparison.OrdinalIgnoreCase) &&
                    !requestPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    // Only covers sit directly in /uploads/courses/{file}. A restored file is served by UseStaticFiles below;
                    // if it could not be saved to disk, the bucket copy is sent from memory instead.
                    var fileName = requestPath.Substring("/uploads/courses/".Length);
                    if (Web.Services.CourseCoverCache.IsCoverFileName(fileName))
                    {
                        var covers = context.RequestServices.GetRequiredService<Web.Services.CourseCoverCache>();
                        var cover = await covers.GetAsync(fileName);
                        if (cover is { LocalPath: null, Bytes: not null })
                        {
                            context.Response.ContentType = cover.ContentType;
                            context.Response.Headers.CacheControl = "public,max-age=86400";
                            await context.Response.Body.WriteAsync(cover.Bytes);
                            return;
                        }
                        if (cover == null && Path.GetExtension(fileName).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp")
                        {
                            // The cover is gone everywhere: show the branded default cover instead of a broken image.
                            var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
                            var defaultCover = Path.Combine(env.WebRootPath, Web.Services.CourseCoverCache.DefaultCoverUrl.TrimStart('/'));
                            context.Response.ContentType = "image/svg+xml";
                            context.Response.Headers.CacheControl = "no-cache";
                            await context.Response.SendFileAsync(defaultCover);
                            return;
                        }
                    }
                }
                await next();
            });

            // 🖼️ Images inside articles (/uploads/posts/content/{file}): restored from the storage bucket after a
            //    redeploy, or sent straight from it when the server folder is not writable.
            app.Use(async (context, next) =>
            {
                const string prefix = "/uploads/posts/content/";
                var requestPath = context.Request.Path.Value ?? string.Empty;
                if (requestPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = requestPath.Substring(prefix.Length);
                    if (Web.Services.CourseCoverCache.IsCoverFileName(fileName))
                    {
                        var store = context.RequestServices.GetRequiredService<Web.Services.CourseCoverCache>();
                        var file = await store.GetAsync(fileName, Web.Services.CourseCoverCache.ArticleImagesDirectory);
                        if (file is { LocalPath: null, Bytes: not null })
                        {
                            context.Response.ContentType = file.ContentType;
                            context.Response.Headers.CacheControl = "public,max-age=86400";
                            await context.Response.Body.WriteAsync(file.Bytes);
                            return;
                        }
                    }
                }
                await next();
            });

            // 🔎 robots.txt and sitemaps are generated live (SeoFilesController), ahead of any stale static copy in wwwroot.
            app.Use(async (context, next) =>
            {
                var requestPath = context.Request.Path.Value ?? string.Empty;
                if (requestPath.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase))
                    context.Request.Path = "/seo-files/robots";
                else if (requestPath.StartsWith("/sitemap", StringComparison.OrdinalIgnoreCase) &&
                         requestPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                         requestPath.LastIndexOf('/') == 0)
                    context.Request.Path = "/seo-files/sitemap";
                await next();
            });

            app.UseStaticFiles();
            app.UseWebMarkupMin();

            var supportedCultures = new[] { "en", "ar", "fa" };
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

            // Student area routing (maps /Students/... to Area: "User")
            app.MapAreaControllerRoute(
                name: "students",
                areaName: "User",
                pattern: "Students/{controller=Dashboard}/{action=Index}/{id?}");

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
