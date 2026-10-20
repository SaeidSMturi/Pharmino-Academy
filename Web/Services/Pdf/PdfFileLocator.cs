namespace Web.Services.Pdf
{
    /// <summary>
    /// Resolves where course PDFs live on disk.
    ///
    /// PDFs are stored OUTSIDE wwwroot so UseStaticFiles() can never serve the clean original.
    /// Default location: {ContentRoot}/AppData/SecurePdfs/courses/{courseId}/pdfs/{file}.
    /// Override with "PdfProtection:StorageRoot" in appsettings.json (e.g. /var/lib/pharmino/pdfs on the VPS).
    ///
    /// Files that still sit in the old public folder (wwwroot/uploads/courses/{id}/pdfs) are moved
    /// into the protected folder automatically the first time they are requested.
    /// </summary>
    public interface IPdfFileLocator
    {
        /// <summary>Folder where new uploads for a course are written.</summary>
        string GetCourseFolder(int courseId);

        /// <summary>Full path of an existing PDF, migrating it out of wwwroot if needed. Null if not found.</summary>
        string? Resolve(int courseId, string fileName);
    }

    public class PdfFileLocator : IPdfFileLocator
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PdfFileLocator> _logger;
        private readonly string _root;
        private static readonly object MigrationLock = new();

        public PdfFileLocator(IWebHostEnvironment env, IConfiguration configuration, ILogger<PdfFileLocator> logger)
        {
            _env = env;
            _logger = logger;

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

        public string? Resolve(int courseId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            // Only ever accept a bare file name (blocks ../ path tricks).
            fileName = Path.GetFileName(fileName);

            var securePath = Path.Combine(GetCourseFolder(courseId), fileName);
            if (File.Exists(securePath)) return securePath;

            var legacyPath = Path.Combine(_env.WebRootPath ?? string.Empty, "uploads", "courses", courseId.ToString(), "pdfs", fileName);
            if (!File.Exists(legacyPath)) return null;

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
                    // Still usable from the old location; the static-file block in Program.cs keeps it private.
                    _logger.LogError(ex, "Could not move PDF out of wwwroot: {Legacy}", legacyPath);
                    return legacyPath;
                }
            }
        }
    }
}
