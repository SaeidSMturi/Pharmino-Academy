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

        /// <summary>Images placed in articles by the post editor (same bucket-backed handling as covers).</summary>
        public const string ArticleImagesDirectory = "uploads/posts/content";

        private readonly IStorageService _storage;
        private readonly ILogger<CourseCoverCache> _logger;
        private readonly string _webRoot;

        public CourseCoverCache(IStorageService storage, IWebHostEnvironment env, ILogger<CourseCoverCache> logger)
        {
            _storage = storage;
            _logger = logger;
            _webRoot = !string.IsNullOrWhiteSpace(env.WebRootPath) ? env.WebRootPath : Path.Combine(env.ContentRootPath, "wwwroot");
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

        private string LocalFolder(string bucketDirectory) => Path.Combine(_webRoot, bucketDirectory.Replace('/', Path.DirectorySeparatorChar));

        public string LocalPath(string fileName, string bucketDirectory = BucketDirectory) => Path.Combine(LocalFolder(bucketDirectory), fileName);

        /// <summary>The cover from disk, or from the bucket when the local copy is missing; null when it exists in neither.</summary>
        public Task<Cover?> GetAsync(string fileName) => GetAsync(fileName, BucketDirectory);

        /// <summary>The same for a file in another bucket-backed upload folder (e.g. <see cref="ArticleImagesDirectory"/>).</summary>
        public async Task<Cover?> GetAsync(string fileName, string bucketDirectory)
        {
            if (!IsCoverFileName(fileName)) return null;

            var contentType = ContentTypeFor(fileName);
            var folder = LocalFolder(bucketDirectory);
            var localPath = Path.Combine(folder, fileName);
            if (File.Exists(localPath)) return new Cover(localPath, null, contentType);
            if (fileName == "default.jpg") return null;

            byte[]? bytes;
            try
            {
                if (!await _storage.FileExistsAsync(bucketDirectory, fileName))
                {
                    _logger.LogWarning("Image {FileName} ({Directory}) is missing both on disk and in the storage bucket; it must be uploaded again.", fileName, bucketDirectory);
                    return null;
                }
                bytes = await _storage.ReadFileBytesAsync(bucketDirectory, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read course cover {FileName} from the storage bucket (check StorageSettings:S3 on the server).", fileName);
                return null;
            }
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                Directory.CreateDirectory(folder);
                // Write to a temp name first so a half-written file is never served.
                var tempPath = localPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllBytesAsync(tempPath, bytes);
                File.Move(tempPath, localPath, overwrite: true);
                _logger.LogInformation("Restored course cover {FileName} from the storage bucket.", fileName);
                return new Cover(localPath, bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Course cover {FileName} was read from the bucket but could not be saved to {Folder} (check the folder's write permission); serving it straight from the bucket.", fileName, folder);
                return new Cover(null, bytes, contentType);
            }
        }
    }
}
