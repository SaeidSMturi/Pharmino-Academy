using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Web.Services.Pdf
{
    /// <summary>
    /// Resolves where course PDFs live on disk and bucket storage.
    ///
    /// PDFs are stored OUTSIDE wwwroot so UseStaticFiles() can never serve the clean original.
    /// Default location: {ContentRoot}/AppData/SecurePdfs/courses/{courseId}/pdfs/{file}.
    /// If not present locally on disk, they are fetched and cached from the S3 / DigitalOcean Spaces bucket.
    /// </summary>
    public interface IPdfFileLocator
    {
        /// <summary>Folder where new uploads for a course are written.</summary>
        string GetCourseFolder(int courseId);

        /// <summary>Full path of an existing PDF, migrating it out of wwwroot or fetching from cloud bucket if needed. Null if not found.</summary>
        string? Resolve(int courseId, string fileName);

        /// <summary>Async version that fetches and caches from bucket if missing locally.</summary>
        Task<string?> ResolveAsync(int courseId, string fileName);
    }

    public class PdfFileLocator : IPdfFileLocator
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PdfFileLocator> _logger;
        private readonly IStorageService _storageService;
        private readonly string _root;
        private static readonly object MigrationLock = new();

        public PdfFileLocator(IWebHostEnvironment env, IConfiguration configuration, ILogger<PdfFileLocator> logger, IStorageService storageService)
        {
            _env = env;
            _logger = logger;
            _storageService = storageService;

            var configured = configuration["PdfProtection:StorageRoot"];
            _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "AppData", "SecurePdfs")
                : Path.Combine(env.ContentRootPath, configured)); // absolute paths are kept as-is by Path.Combine

            // Safety net: refuse to store protected PDFs anywhere under wwwroot.
            var webRoot = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
            if (Path.GetFullPath(_root).StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PdfProtection:StorageRoot must not be inside wwwroot.");
        }

        public string GetCourseFolder(int courseId)
        {
            var folder = Path.Combine(_root, "courses", courseId.ToString(), "pdfs");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public async Task<string?> ResolveAsync(int courseId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            // Only ever accept a bare file name (blocks ../ path tricks).
            fileName = Path.GetFileName(fileName);

            var securePath = Path.Combine(GetCourseFolder(courseId), fileName);
            if (File.Exists(securePath)) return securePath;

            var legacyPath = Path.Combine(_env.WebRootPath ?? string.Empty, "uploads", "courses", courseId.ToString(), "pdfs", fileName);
            if (File.Exists(legacyPath))
            {
                lock (MigrationLock)
                {
                    if (File.Exists(securePath)) return securePath;
                    try
                    {
                        File.Move(legacyPath, securePath);
                        _logger.LogInformation("Moved PDF out of wwwroot: {Legacy} -> {Secure}", legacyPath, securePath);
                        return securePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not move PDF out of wwwroot: {Legacy}", legacyPath);
                        return legacyPath;
                    }
                }
            }

            // Check cloud bucket storage (DigitalOcean Spaces / S3)
            try
            {
                var bucketDirectory = $"uploads/courses/{courseId}/pdfs";
                if (await _storageService.FileExistsAsync(bucketDirectory, fileName))
                {
                    var fileBytes = await _storageService.ReadFileBytesAsync(bucketDirectory, fileName);
                    if (fileBytes != null && fileBytes.Length > 0)
                    {
                        await File.WriteAllBytesAsync(securePath, fileBytes);
                        _logger.LogInformation("Resolved and cached PDF from cloud bucket: {Dir}/{File} -> {Path}", bucketDirectory, fileName, securePath);
                        return securePath;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloud bucket lookup failed for PDF file: {FileName} in Course {CourseId}", fileName, courseId);
            }

            return null;
        }

        public string? Resolve(int courseId, string fileName)
        {
            return ResolveAsync(courseId, fileName).GetAwaiter().GetResult();
        }
    }
}
