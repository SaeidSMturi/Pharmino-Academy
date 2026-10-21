using Application.Interfaces;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text;

namespace Web.Areas.User.Controllers
{
    /// <summary>
    /// ── فاز ۲: کنترلر امن HLS ──
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
        private readonly IMemoryCache _cache;

        // TTL توکن‌ها
        private const int SegmentTokenTtlSeconds = 120;  // ۲ دقیقه برای هر چانک
        private const int KeyTokenTtlMinutes = 5;        // ۵ دقیقه برای کلید AES

        public HlsController(
            ICourseService courseService,
            IVideoTokenService videoTokenService,
            IVideoSessionManager sessionManager,
            IVideoSegmentationService segmentationService,
            IHlsProcessingService hlsService,
            ILogger<HlsController> logger,
            IMemoryCache cache)
        {
            _courseService = courseService;
            _videoTokenService = videoTokenService;
            _sessionManager = sessionManager;
            _segmentationService = segmentationService;
            _hlsService = hlsService;
            _logger = logger;
            _cache = cache;
        }

        private async Task<(int CourseId, string VideoGuid)?> GetCachedVideoMetaAsync(int videoId)
        {
            string cacheKey = $"Hls_VideoMeta_{videoId}";
            if (_cache.TryGetValue(cacheKey, out (int CourseId, string VideoGuid) cached))
            {
                return cached;
            }

            var video = await _courseService.GetVideoByIdAsync(videoId);
            if (video == null) return null;

            var meta = (video.CourseId, Path.GetFileNameWithoutExtension(video.VideoFileName));
            _cache.Set(cacheKey, meta, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
                Size = 1
            });

            return meta;
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
        /// سرو پلی‌لیست HLS با URL‌های امضاشده per-segment یا Master Playlist برای کیفیت‌های چندگانه.
        /// پلی‌لیست اصلی روی دیسک هرگز مستقیم serve نمی‌شود.
        /// MIME: application/vnd.apple.mpegurl
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Manifest(int videoId, string sid, string ct, string? quality = null)
        {
            var userId = GetCurrentUserId();

            if (!await ValidateFullAccessAsync(userId, videoId, sid, ct))
            {
                _logger.LogWarning(
                    "🚫 Manifest access denied — VideoId: {VideoId}, UserId: {UserId}",
                    videoId, userId);
                return Unauthorized(new { error = "Access denied" });
            }

            var meta = await GetCachedVideoMetaAsync(videoId);
            if (meta == null) return NotFound();

            if (!await _hlsService.HlsExistsAsync(meta.Value.CourseId, meta.Value.VideoGuid))
            {
                _logger.LogWarning(
                    "HLS files not found — VideoId: {VideoId}, Guid: {Guid}",
                    videoId, meta.Value.VideoGuid);
                return NotFound(new { error = "HLS stream not available for this video. Please contact support." });
            }

            string rawPlaylist;
            if (string.IsNullOrEmpty(quality))
            {
                rawPlaylist = await _hlsService.ReadPlaylistAsync(meta.Value.CourseId, meta.Value.VideoGuid);

                // اگر پلی‌لیست اصلی شامل شاخه‌های کیفیت چندگانه باشد، مستر پلی‌لیست بازنویسی می‌شود
                if (rawPlaylist.Contains("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
                {
                    var secureMaster = RewriteMasterPlaylist(rawPlaylist, videoId, sid, ct);
                    _logger.LogInformation("✅ Multi-Quality HLS Master manifest served — VideoId: {VideoId}, UserId: {UserId}", videoId, userId);
                    SetSecurityHeaders();
                    return Content(secureMaster, "application/vnd.apple.mpegurl", Encoding.UTF8);
                }

                // پلی‌لیست تک‌کیفیت سنتی
                var securePlaylist = RewritePlaylist(rawPlaylist, videoId, meta.Value.CourseId, meta.Value.VideoGuid, userId, sid, null);
                _logger.LogInformation("✅ HLS single manifest served — VideoId: {VideoId}, UserId: {UserId}", videoId, userId);
                SetSecurityHeaders();
                return Content(securePlaylist, "application/vnd.apple.mpegurl", Encoding.UTF8);
            }
            else
            {
                // خواندن ساب‌پلی‌لیست مربوط به کیفیت درخواستی (مثلاً 1080p, 720p, 480p)
                rawPlaylist = await _hlsService.ReadVariantPlaylistAsync(meta.Value.CourseId, meta.Value.VideoGuid, quality);
                var secureVariantPlaylist = RewritePlaylist(rawPlaylist, videoId, meta.Value.CourseId, meta.Value.VideoGuid, userId, sid, quality);
                _logger.LogInformation("✅ HLS variant manifest served for {Quality} — VideoId: {VideoId}, UserId: {UserId}", quality, videoId, userId);
                SetSecurityHeaders();
                return Content(secureVariantPlaylist, "application/vnd.apple.mpegurl", Encoding.UTF8);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 🎬 Segment — چانک .ts رمزنگاری‌شده
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// سرو یک چانک .ts پس از اعتبارسنجی توکن per-segment و session.
        /// MIME: video/mp2t
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Segment(int videoId, int seg, string sid, string st, string? quality = null)
        {
            var userId = GetCurrentUserId();

            if (!ValidateSegmentAccess(userId, videoId, seg, sid, st))
            {
                _logger.LogWarning("🚫 Segment denied — VideoId: {VideoId}, Seg: {Seg}, Quality: {Quality}", videoId, seg, quality);
                return Unauthorized(new { error = "Invalid segment token" });
            }

            if (!Request.Headers.TryGetValue("X-Video-Session", out var headerSid) || headerSid.ToString() != sid)
            {
                return Unauthorized(new { error = "Direct URL access not allowed" });
            }

            var meta = await GetCachedVideoMetaAsync(videoId);
            if (meta == null) return NotFound();

            try
            {
                // دریافت مستقیم استریم چانک با توجه به کیفیت انتخابی از باکت / دیسک
                var (fileStream, contentLength) = await _hlsService.GetSegmentStreamAsync(meta.Value.CourseId, meta.Value.VideoGuid, seg, quality);

                _sessionManager.RecordChunkRequest(sid, 0, contentLength, contentLength, TimeSpan.FromMilliseconds(1));

                SetSecurityHeaders();
                if (contentLength > 0)
                {
                    Response.ContentLength = contentLength;
                }

                return File(fileStream, "video/mp2t");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Segment file not found in Storage — VideoId: {VideoId}, Seg: {Seg}, Quality: {Quality}", videoId, seg, quality);
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

            var meta = await GetCachedVideoMetaAsync(videoId);
            if (meta == null) return NotFound();

            try
            {
                // 🌟 خواندن مستقیم کلید از باکت
                var keyBytes = await _hlsService.ReadKeyAsync(meta.Value.CourseId, meta.Value.VideoGuid);

                Response.Headers.Append("Cache-Control", "private, no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("X-Content-Type-Options", "nosniff");

                return File(keyBytes, "application/octet-stream");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AES key file missing in Storage — VideoId: {VideoId}, Guid: {Guid}", videoId, meta.Value.VideoGuid);
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
        // 📝 بازنویسی پلی‌لیست مستر و ساب‌پلی‌لیست‌ها
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// پلی‌لیست مستر شامل شاخه‌های کیفیت‌های مختلف (1080p, 720p, 480p) را با لینک‌های امن Manifest بازنویسی می‌کند.
        /// </summary>
        private string RewriteMasterPlaylist(string rawMaster, int videoId, string sid, string ct)
        {
            var lines = rawMaster.Split('\n');
            var result = new StringBuilder(rawMaster.Length * 2);

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (line.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("#"))
                {
                    // نام کیفیت بر اساس نام فایل پلی‌لیست (مثلاً 1080p یا 720p یا 480p)
                    var qualityName = Path.GetFileNameWithoutExtension(line);
                    var variantUrl = Url.Action("Manifest", "Hls", new
                    {
                        Area = "User",
                        videoId,
                        sid,
                        ct,
                        quality = qualityName
                    }, Request.Scheme)!;

                    result.AppendLine(variantUrl);
                }
                else
                {
                    result.AppendLine(line);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// پلی‌لیست اصلی FFmpeg را با URL‌های امن جایگزین می‌کند:
        ///   - URI کلید AES در EXT-X-KEY با endpoint /User/Hls/Key جایگزین می‌شود
        ///   - هر مسیر seg00X.ts با URL امضاشده /User/Hls/Segment جایگزین می‌شود
        /// </summary>
        private string RewritePlaylist(
            string rawPlaylist,
            int videoId, int courseId, string videoGuid,
            int userId, string sid,
            string? quality)
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
                if (line.EndsWith(HlsProcessingService.SegmentExtension, StringComparison.OrdinalIgnoreCase)
                    && !line.StartsWith("#"))
                {
                    // TTL شش ساعت، اعتبارسنجی زنده توسط SessionManager انجام می‌شود
                    var segToken = _segmentationService.GenerateSegmentToken(
                        videoId, userId, segIndex,
                        DateTime.UtcNow.AddHours(6));

                    var segUrl = Url.Action("Segment", "Hls", new
                    {
                        Area = "User",
                        videoId,
                        seg = segIndex,
                        sid,
                        st = segToken,
                        quality = quality
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
