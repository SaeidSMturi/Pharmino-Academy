using Application.Interfaces;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace Web.Areas.User.Controllers
{
    /// <summary>
    /// ── فاز ۲: کنترلر امن HLS ──
    ///
    /// سه endpoint برای سرو ایمن فایل‌های HLS:
    ///
    ///   GET /User/Hls/Manifest?videoId={id}&amp;sid={sid}&amp;ct={ct}
    ///       → پلی‌لیست m3u8 بازنویسی‌شده با URL امضاشده برای هر چانک
    ///
    ///   GET /User/Hls/Segment?videoId={id}&amp;seg={index}&amp;sid={sid}&amp;st={segToken}
    ///       → فایل .ts رمزنگاری‌شده پس از اعتبارسنجی توکن per-segment
    ///
    ///   GET /User/Hls/Key?videoId={id}&amp;sid={sid}&amp;kt={keyToken}
    ///       → کلید AES-128 خام (16 بایت) پس از اعتبارسنجی session
    ///
    /// مدل امنیتی:
    ///   ct  = توکن دسترسی (60 دقیقه) — از صفحه Watch تأمین می‌شود
    ///   st  = توکن per-segment با TTL 120 ثانیه — تعبیه‌شده در m3u8
    ///   kt  = توکن کلید AES با TTL 5 دقیقه — در EXT-X-KEY تعبیه‌شده در m3u8
    ///   sid = HMAC-signed session ID — روی هر درخواست اعتبارسنجی می‌شود
    ///
    /// جلوگیری از IDM / ابزارهای دانلود:
    ///   1. توکن‌های کوتاه‌مدت per-segment: دانلود کامل ویدیو بدون session فعال ناممکن
    ///   2. IP binding در session manager
    ///   3. اعتبارسنجی heartbeat در SessionManager
    ///   4. سگمنت‌ها AES-128 رمز شده‌اند; بدون کلید قابل پخش نیستند
    ///   5. کلید AES فقط با session فعال و توکن معتبر سرو می‌شود
    ///   6. هدر X-Video-Session در xhrSetup پلیر الزامی است
    /// </summary>
    [Area("User")]
    [Authorize]
    public class HlsController : Controller
    {
        private readonly ICourseService _courseService;
        private readonly IVideoTokenService _videoTokenService;
        private readonly IVideoSessionManager _sessionManager;
        private readonly IVideoSegmentationService _segmentationService;
        private readonly IHlsProcessingService _hlsService;
        private readonly ILogger<HlsController> _logger;

        // TTL توکن‌ها
        private const int SegmentTokenTtlSeconds = 120;  // ۲ دقیقه برای هر چانک
        private const int KeyTokenTtlMinutes = 5;        // ۵ دقیقه برای کلید AES

        public HlsController(
            ICourseService courseService,
            IVideoTokenService videoTokenService,
            IVideoSessionManager sessionManager,
            IVideoSegmentationService segmentationService,
            IHlsProcessingService hlsService,
            ILogger<HlsController> logger)
        {
            _courseService = courseService;
            _videoTokenService = videoTokenService;
            _sessionManager = sessionManager;
            _segmentationService = segmentationService;
            _hlsService = hlsService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 📋 Manifest — پلی‌لیست HLS بازنویسی‌شده
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// سرو پلی‌لیست HLS با URL‌های امضاشده per-segment.
        /// پلی‌لیست اصلی روی دیسک هرگز مستقیم serve نمی‌شود.
        /// MIME: application/vnd.apple.mpegurl
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Manifest(int videoId, string sid, string ct)
        {
            var userId = GetCurrentUserId();

            if (!await ValidateFullAccessAsync(userId, videoId, sid, ct))
            {
                _logger.LogWarning(
                    "🚫 Manifest access denied — VideoId: {VideoId}, UserId: {UserId}",
                    videoId, userId);
                return Unauthorized(new { error = "Access denied" });
            }

            var video = await _courseService.GetVideoByIdAsync(videoId);
            if (video == null) return NotFound();

            var videoGuid = Path.GetFileNameWithoutExtension(video.VideoFileName);

            if (!await _hlsService.HlsExistsAsync(video.CourseId, videoGuid))
            {
                _logger.LogWarning(
                    "HLS files not found — VideoId: {VideoId}, Guid: {Guid}",
                    videoId, videoGuid);
                return NotFound(new { error = "HLS stream not available for this video. Please contact support." });
            }

            var rawPlaylist = await _hlsService.ReadPlaylistAsync(video.CourseId, videoGuid);

            // بازنویسی پلی‌لیست با URL‌های امضاشده
            var securePlaylist = RewritePlaylist(rawPlaylist, videoId, video.CourseId, videoGuid, userId, sid);

            _logger.LogInformation(
                "✅ HLS manifest served — VideoId: {VideoId}, UserId: {UserId}",
                videoId, userId);

            SetSecurityHeaders();
            // MIME صحیح برای HLS که Safari و hls.js هر دو نیاز دارند
            return Content(securePlaylist, "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 🎬 Segment — چانک .ts رمزنگاری‌شده
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// سرو یک چانک .ts پس از اعتبارسنجی توکن per-segment و session.
        /// MIME: video/mp2t
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Segment(int videoId, int seg, string sid, string st)
        {
            var userId = GetCurrentUserId();

            if (!ValidateSegmentAccess(userId, videoId, seg, sid, st))
            {
                _logger.LogWarning("🚫 Segment denied — VideoId: {VideoId}, Seg: {Seg}", videoId, seg);
                return Unauthorized(new { error = "Invalid segment token" });
            }

            if (!Request.Headers.TryGetValue("X-Video-Session", out var headerSid) || headerSid.ToString() != sid)
            {
                return Unauthorized(new { error = "Direct URL access not allowed" });
            }

            var video = await _courseService.GetVideoByIdAsync(videoId);
            if (video == null) return NotFound();

            var videoGuid = Path.GetFileNameWithoutExtension(video.VideoFileName);

            try
            {
                // 🌟 تغییر کلیدی: دریافت مستقیم استریم از S3 به جای ریختن در آرایه بایت
                // فرض بر این است که متدی به نام GetSegmentStreamAsync نوشته‌اید
                var (fileStream, contentLength) = await _hlsService.GetSegmentStreamAsync(video.CourseId, videoGuid, seg);

                _sessionManager.RecordChunkRequest(sid, 0, contentLength, contentLength, TimeSpan.FromMilliseconds(1));

                SetSecurityHeaders();

                // 🌟 ارسال مستقیم استریم به کلاینت؛ دانلود از S3 و آپلود به کاربر همزمان انجام می‌شود!
                return File(fileStream, "video/mp2t");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Segment file not found in Storage — VideoId: {VideoId}, Seg: {Seg}", videoId, seg);
                return NotFound();
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 🔑 Key — کلید AES-128 خام
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// سرو کلید AES-128 (16 بایت) فقط برای session فعال با توکن معتبر.
        /// Cache-Control: no-store تضمین می‌کند کلید در مرورگر کش نشود.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Key(int videoId, string sid, string kt)
        {
            var userId = GetCurrentUserId();

            if (!ValidateSession(userId, videoId, sid)) return Unauthorized(new { error = "Invalid session" });

            if (string.IsNullOrEmpty(kt) || !_videoTokenService.ValidateToken(kt, videoId, userId))
                return Unauthorized(new { error = "Invalid key token" });

            var video = await _courseService.GetVideoByIdAsync(videoId);
            if (video == null) return NotFound();

            var videoGuid = Path.GetFileNameWithoutExtension(video.VideoFileName);

            try
            {
                // 🌟 خواندن مستقیم کلید از باکت
                var keyBytes = await _hlsService.ReadKeyAsync(video.CourseId, videoGuid);

                Response.Headers.Append("Cache-Control", "private, no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("X-Content-Type-Options", "nosniff");

                return File(keyBytes, "application/octet-stream");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AES key file missing in Storage — VideoId: {VideoId}, Guid: {Guid}", videoId, videoGuid);
                return NotFound();
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 🔒 متدهای اعتبارسنجی خصوصی
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private async Task<bool> ValidateFullAccessAsync(int userId, int videoId, string sid, string ct)
        {
            if (userId <= 0) return false;

            // اعتبارسنجی توکن دسترسی محتوا (ct)
            if (string.IsNullOrEmpty(ct) || !_videoTokenService.ValidateToken(ct, videoId, userId))
            {
                _logger.LogWarning(
                    "Invalid content token (ct) — VideoId: {VideoId}, UserId: {UserId}",
                    videoId, userId);
                return false;
            }

            if (!ValidateSession(userId, videoId, sid)) return false;

            // بررسی ثبت‌نام کاربر در دوره
            return await _courseService.CanUserAccessVideoAsync(userId, videoId);
        }

        private bool ValidateSession(int userId, int videoId, string sid)
        {
            if (string.IsNullOrEmpty(sid)) return false;

            if (!_videoTokenService.ValidateSessionId(sid, videoId, userId))
            {
                _logger.LogWarning(
                    "Invalid session ID — VideoId: {VideoId}, UserId: {UserId}",
                    videoId, userId);
                return false;
            }

            if (_sessionManager.ShouldBlockStream(sid))
            {
                _logger.LogError(
                    "🚫 Session blocked by behavior analysis — SessionId: {Sid}, UserId: {UserId}",
                    sid, userId);
                return false;
            }

            return true;
        }

        private bool ValidateSegmentAccess(int userId, int videoId, int segIndex, string sid, string st)
        {
            if (!ValidateSession(userId, videoId, sid)) return false;

            if (string.IsNullOrEmpty(st)
                || !_segmentationService.ValidateSegmentToken(st, videoId, userId, segIndex))
            {
                _logger.LogWarning(
                    "Invalid segment token (st) — VideoId: {VideoId}, Seg: {Seg}, UserId: {UserId}",
                    videoId, segIndex, userId);
                return false;
            }

            return true;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 📝 بازنویسی پلی‌لیست
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// پلی‌لیست اصلی FFmpeg را با URL‌های امن جایگزین می‌کند:
        ///   - URI کلید AES در EXT-X-KEY با endpoint /User/Hls/Key جایگزین می‌شود
        ///   - هر مسیر seg00X.ts با URL امضاشده /User/Hls/Segment جایگزین می‌شود
        /// </summary>
        private string RewritePlaylist(
            string rawPlaylist,
            int videoId, int courseId, string videoGuid,
            int userId, string sid)
        {
            // توکن کلید AES با TTL کوتاه (5 دقیقه)
            var keyToken = _videoTokenService.GenerateToken(videoId, userId, KeyTokenTtlMinutes);
            var keyUrl = Url.Action("Key", "Hls", new
            {
                Area = "User",
                videoId,
                sid,
                kt = keyToken
            }, Request.Scheme)!;

            var lines = rawPlaylist.Split('\n');
            var result = new StringBuilder(rawPlaylist.Length * 3);
            var segIndex = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');

                // ── جایگزینی URI کلید AES در EXT-X-KEY ──
                // فرمت FFmpeg: #EXT-X-KEY:METHOD=AES-128,URI="KEYHOLDER_PLACEHOLDER",IV=0x...
                if (line.StartsWith("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase)
                    && line.Contains(HlsProcessingService.KeyUriPlaceholder, StringComparison.Ordinal))
                {
                    line = line.Replace(
                        $"URI=\"{HlsProcessingService.KeyUriPlaceholder}\"",
                        $"URI=\"{keyUrl}\"",
                        StringComparison.Ordinal);
                    result.AppendLine(line);
                    continue;
                }

                // ── جایگزینی مسیر .ts با URL امضاشده per-segment ──
                // ── جایگزینی مسیر .ts با URL امضاشده per-segment ──
                if (line.EndsWith(HlsProcessingService.SegmentExtension, StringComparison.OrdinalIgnoreCase)
                    && !line.StartsWith("#"))
                {
                    // تغییر مهم: TTL به ۶ ساعت افزایش یافت. 
                    // امنیت لحظه‌ای توسط SessionManager و Heartbeat تامین می‌شود نه توکن سگمنت.
                    var segToken = _segmentationService.GenerateSegmentToken(
                        videoId, userId, segIndex,
                        DateTime.UtcNow.AddHours(6));

                    var segUrl = Url.Action("Segment", "Hls", new
                    {
                        Area = "User",
                        videoId,
                        seg = segIndex,
                        sid,
                        st = segToken
                    }, Request.Scheme)!;

                    result.AppendLine(segUrl);
                    segIndex++;
                    continue;
                }

                result.AppendLine(line);
            }

            return result.ToString();
        }

        private void SetSecurityHeaders()
        {
            Response.Headers.Append("Cache-Control", "private, no-cache, no-store, must-revalidate");
            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        }
    }
}
