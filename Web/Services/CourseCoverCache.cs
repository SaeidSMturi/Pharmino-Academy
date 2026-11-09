using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Web.Services
{
    /// <summary>
    /// Course covers live in the storage bucket (uploads/courses/{file}) with a copy in wwwroot/uploads/courses.
    /// A redeploy wipes the local copies, so anything that serves a cover calls <see cref="GetAsync"/> first:
    /// it puts the bucket copy back on disk when the local one is missing, and if the server cannot write
    /// there it still hands back the bytes so the cover can be served straight from the bucket.
    /// </summary>
    public sealed class CourseCoverCache
    {
        public const string BucketDirectory = "uploads/courses";

        /// <summary>Shown wherever a course has no usable cover.</summary>
        public const string DefaultCoverUrl = "/images/brand/course-cover-default.svg";

        /// <summary>A cover found on disk (LocalPath) or, when it could not be saved there, only in memory (Bytes).</summary>
        public sealed record Cover(string? LocalPath, byte[]? Bytes, string ContentType);

        private readonly IStorageService _storage;
        private readonly ILogger<CourseCoverCache> _logger;
        private readonly string _folder;

        public CourseCoverCache(IStorageService storage, IWebHostEnvironment env, ILogger<CourseCoverCache> logger)
        {
            _storage = storage;
            _logger = logger;
            var webRoot = !string.IsNullOrWhiteSpace(env.WebRootPath) ? env.WebRootPath : Path.Combine(env.ContentRootPath, "wwwroot");
            _folder = Path.Combine(webRoot, "uploads", "courses");
        }

        /// <summary>A plain cover file name (no folders, no "..").</summary>
        public static bool IsCoverFileName(string? fileName) =>
            !string.IsNullOrWhiteSpace(fileName) &&
            fileName == Path.GetFileName(fileName) &&
            !fileName.Contains("..") &&
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        public static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        public string LocalPath(string fileName) => Path.Combine(_folder, fileName);

        /// <summary>The cover from disk, or from the bucket when the local copy is missing; null when it exists in neither.</summary>
        public async Task<Cover?> GetAsync(string fileName)
        {
            if (!IsCoverFileName(fileName)) return null;

            var contentType = ContentTypeFor(fileName);
            var localPath = LocalPath(fileName);
            if (File.Exists(localPath)) return new Cover(localPath, null, contentType);
            if (fileName == "default.jpg") return null;

            byte[]? bytes;
            try
            {
                if (!await _storage.FileExistsAsync(BucketDirectory, fileName))
                {
                    _logger.LogWarning("Course cover {FileName} is missing both on disk and in the storage bucket; it must be uploaded again.", fileName);
                    return null;
                }
                bytes = await _storage.ReadFileBytesAsync(BucketDirectory, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read course cover {FileName} from the storage bucket (check StorageSettings:S3 on the server).", fileName);
                return null;
            }
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                Directory.CreateDirectory(_folder);
                // Write to a temp name first so a half-written file is never served.
                var tempPath = localPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllBytesAsync(tempPath, bytes);
                File.Move(tempPath, localPath, overwrite: true);
                _logger.LogInformation("Restored course cover {FileName} from the storage bucket.", fileName);
                return new Cover(localPath, bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Course cover {FileName} was read from the bucket but could not be saved to {Folder} (check the folder's write permission); serving it straight from the bucket.", fileName, _folder);
                return new Cover(null, bytes, contentType);
            }
        }
    }
}
