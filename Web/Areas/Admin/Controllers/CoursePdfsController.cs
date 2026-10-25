using Application.Interfaces;
using Domain.Configuration;
using Infrastructure.Persistence;
using Domain.Entities.Course;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Web.Attributes;
using Web.Services.Pdf;

namespace Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class CoursePdfsController : AdminBaseController
{
    private readonly WebContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly UploadLimitsOptions _uploadLimits;
    private readonly ILogger<CoursePdfsController> _logger;
    private readonly IPdfFileLocator _pdfLocator;
    private readonly IStorageService _storageService;

    public class IdRequest
    {
        public int Id { get; set; }
    }

    public class DeletePdfRequest
    {
        public int Id { get; set; }
    }

    public CoursePdfsController(
        WebContext context,
        IWebHostEnvironment environment,
        Microsoft.Extensions.Options.IOptions<UploadLimitsOptions> uploadLimits,
        ILogger<CoursePdfsController> logger,
        IPdfFileLocator pdfLocator,
        IStorageService storageService)
    {
        _context = context;
        _environment = environment;
        _uploadLimits = uploadLimits.Value;
        _logger = logger;
        _pdfLocator = pdfLocator;
        _storageService = storageService;
    }

    [Permission("Courses.View")]
    public async Task<IActionResult> Index(int courseId)
    {
        _logger.LogInformation("Fetching PDF list for Course ID: {CourseId}", courseId);

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null)
        {
            _logger.LogWarning("Course with ID {CourseId} not found when requesting PDF list.", courseId);
            TempData["ErrorMessage"] = "Course not found";
            return RedirectToAction("Index", "Courses");
        }

        var pdfs = await _context.CoursePdfs
            .Where(p => p.CourseId == courseId)
            .OrderByDescending(p => p.CreateDate)
            .ToListAsync();

        _logger.LogInformation("Successfully retrieved {Count} PDFs for Course ID: {CourseId} ({CourseTitle})", pdfs.Count, courseId, course.Title);

        ViewBag.CourseId = courseId;
        ViewBag.CourseTitle = course.Title;

        return View(pdfs);
    }

    [Permission("Courses.View")]
    public async Task<IActionResult> Download(int id)
    {
        _logger.LogInformation("Download requested for PDF ID: {PdfId} by User: {Username}", id, User.Identity?.Name ?? "Unknown");

        var pdf = await _context.CoursePdfs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PdfId == id);

        if (pdf == null)
        {
            _logger.LogWarning("Download failed. PDF ID {PdfId} does not exist.", id);
            TempData["ErrorMessage"] = "PDF not found";
            return RedirectToAction("Index", "Courses");
        }

        var pdfPath = _pdfLocator.Resolve(pdf.CourseId, pdf.FileName);

        if (pdfPath == null)
        {
            _logger.LogError("Physical PDF file not found on disk. File: {FileName}, PDF ID: {PdfId}", pdf.FileName, id);
            TempData["ErrorMessage"] = "PDF file not found on server";
            return RedirectToAction("Index", new { courseId = pdf.CourseId });
        }

        var downloadName = string.IsNullOrWhiteSpace(pdf.OriginalFileName) ? "file.pdf" : pdf.OriginalFileName;
        _logger.LogInformation("Serving Physical File for download. PDF ID: {PdfId}, Original Name: {DownloadName}", id, downloadName);

        return PhysicalFile(pdfPath, "application/pdf", downloadName);
    }

    [Permission("Courses.View")]
    public async Task<IActionResult> Create(int courseId)
    {
        _logger.LogInformation("Navigating to PDF creation page for Course ID: {CourseId}", courseId);

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null)
        {
            _logger.LogWarning("Cannot open create PDF view. Course ID {CourseId} not found.", courseId);
            TempData["ErrorMessage"] = "Course not found";
            return RedirectToAction("Index", "Courses");
        }

        ViewBag.CourseId = courseId;
        ViewBag.CourseTitle = course.Title;

        return View(new CoursePdf { CourseId = courseId, IsActive = true, Price = 0 });
    }

    [HttpPost]
    [Permission("Courses.Create")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(209715200)] // 200MB for PDF upload
    public async Task<IActionResult> Create([Bind(nameof(CoursePdf.CourseId), nameof(CoursePdf.Title), nameof(CoursePdf.Price), nameof(CoursePdf.IsActive))] CoursePdf pdf, IFormFile? PdfFile)
    {
        _logger.LogInformation("Attempting to upload a new PDF for Course ID: {CourseId}. Title: {Title}", pdf.CourseId, pdf.Title);

        pdf.Title = pdf.Title?.Trim();

        ModelState.Remove("FileName");
        ModelState.Remove("OriginalFileName");
        ModelState.Remove("Course");
        ModelState.Remove("CreateDate");
        ModelState.Remove("LastUpdate");

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("PDF creation failed due to invalid ModelState for Course ID: {CourseId}.", pdf.CourseId);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == pdf.CourseId);
            if (course != null)
            {
                ViewBag.CourseId = pdf.CourseId;
                ViewBag.CourseTitle = course.Title;
            }
            return View(pdf);
        }

        if (PdfFile == null || PdfFile.Length == 0)
        {
            _logger.LogWarning("PDF creation failed. No file uploaded or file is empty for Course ID: {CourseId}.", pdf.CourseId);
            TempData["ErrorMessage"] = "Please select a PDF file";
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == pdf.CourseId);
            if (course != null)
            {
                ViewBag.CourseId = pdf.CourseId;
                ViewBag.CourseTitle = course.Title;
            }
            return View(pdf);
        }

        if (PdfFile.Length > _uploadLimits.CoursePdfMaxBytes)
        {
            _logger.LogWarning("PDF creation failed. File size {Length} bytes exceeds limit {Limit} bytes for Course ID: {CourseId}.",
                PdfFile.Length, _uploadLimits.CoursePdfMaxBytes, pdf.CourseId);
            TempData["ErrorMessage"] = "PDF size exceeds the allowed limit";
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == pdf.CourseId);
            if (course != null)
            {
                ViewBag.CourseId = pdf.CourseId;
                ViewBag.CourseTitle = course.Title;
            }
            return View(pdf);
        }

        var fileExtension = Path.GetExtension(PdfFile.FileName).ToLowerInvariant();
        if (fileExtension != ".pdf")
        {
            _logger.LogWarning("PDF creation failed. Invalid extension '{Extension}' for Course ID: {CourseId}.", fileExtension, pdf.CourseId);
            TempData["ErrorMessage"] = "Only PDF files are allowed";
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == pdf.CourseId);
            if (course != null)
            {
                ViewBag.CourseId = pdf.CourseId;
                ViewBag.CourseTitle = course.Title;
            }
            return View(pdf);
        }

        // Protected folder outside wwwroot (see Web/Services/Pdf/PdfFileLocator.cs)
        string uploadsFolder;

        try
        {
            uploadsFolder = _pdfLocator.GetCourseFolder(pdf.CourseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create protected PDF directory for Course ID: {CourseId}", pdf.CourseId);
            TempData["ErrorMessage"] = "Server error creating directory structure.";
            ViewBag.CourseId = pdf.CourseId;
            ViewBag.CourseTitle = (await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == pdf.CourseId))?.Title ?? string.Empty;
            return View(pdf);
        }

        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await PdfFile.CopyToAsync(fileStream);
            }
            _logger.LogInformation("Physical file saved successfully to path: {FilePath}", filePath);

            // Upload PDF to cloud bucket storage (DigitalOcean Spaces / S3)
            try
            {
                var bucketDir = $"uploads/courses/{pdf.CourseId}/pdfs";
                await _storageService.UploadPhysicalFileAsync(filePath, bucketDir, uniqueFileName);
                _logger.LogInformation("PDF uploaded successfully to cloud bucket: {BucketDir}/{FileName}", bucketDir, uniqueFileName);
            }
            catch (Exception cloudEx)
            {
                _logger.LogError(cloudEx, "Warning: Failed to upload PDF {FileName} to cloud bucket, but local copy succeeded.", uniqueFileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save physical file to disk. Path: {FilePath}", filePath);
            TempData["ErrorMessage"] = "Failed to write file to server.";
            ViewBag.CourseId = pdf.CourseId;
            ViewBag.CourseTitle = (await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == pdf.CourseId))?.Title ?? string.Empty;
            return View(pdf);
        }

        pdf.FileName = uniqueFileName;
        pdf.OriginalFileName = Path.GetFileName(PdfFile.FileName);
        if (string.IsNullOrWhiteSpace(pdf.Title))
        {
            pdf.Title = Path.GetFileNameWithoutExtension(pdf.OriginalFileName);
        }
        pdf.CreateDate = DateTime.UtcNow;

        try
        {
            _context.CoursePdfs.Add(pdf);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Database record for PDF created successfully. PDF ID: {PdfId}, Course ID: {CourseId}", pdf.PdfId, pdf.CourseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database save failed for PDF metadata. Initiating physical file cleanup for: {FilePath}", filePath);
            TempData["ErrorMessage"] = $"Failed to save PDF: {ex.Message}";

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation("Cleanup: Physical file deleted successfully after DB failure.");
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Cleanup Failed: Could not delete file: {FilePath}", filePath);
                }
            }

            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == pdf.CourseId);
            if (course != null)
            {
                ViewBag.CourseId = pdf.CourseId;
                ViewBag.CourseTitle = course.Title;
            }
            return View(pdf);
        }

        TempData["SuccessMessage"] = "PDF added successfully";
        return RedirectToAction("Details", "Courses", new { id = pdf.CourseId });
    }

    [HttpPost]
    [Permission("Courses.Delete")]
    public async Task<IActionResult> Delete([FromBody] DeletePdfRequest? payload, int? id)
    {
        var requestedId = payload?.Id > 0 ? payload!.Id : (id ?? 0);
        _logger.LogInformation("Delete requested for PDF ID: {PdfId} by User: {Username}", requestedId, User.Identity?.Name ?? "Unknown");

        try
        {
            if (requestedId <= 0)
            {
                _logger.LogWarning("Delete aborted. Invalid PDF ID provided: {PdfId}", requestedId);
                return Json(new { success = false, message = "Invalid pdf id" });
            }

            var pdf = await _context.CoursePdfs.FirstOrDefaultAsync(p => p.PdfId == requestedId);
            if (pdf == null)
            {
                _logger.LogWarning("Delete failed. PDF record with ID {PdfId} not found in database.", requestedId);
                return Json(new { success = false, message = "PDF not found" });
            }

            if (!string.IsNullOrWhiteSpace(pdf.FileName))
            {
                var pdfPath = _pdfLocator.Resolve(pdf.CourseId, pdf.FileName);
                if (pdfPath != null)
                {
                    try
                    {
                        System.IO.File.Delete(pdfPath);
                        _logger.LogInformation("Physical file deleted from disk: {PdfPath}", pdfPath);
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogError(fileEx, "Failed to delete physical file from disk during record deletion. Path: {PdfPath}", pdfPath);
                    }
                }
                else
                {
                    _logger.LogWarning("Physical file not found on disk during deletion. File: {FileName}", pdf.FileName);
                }

                // Delete from cloud bucket storage
                try
                {
                    var bucketDir = $"uploads/courses/{pdf.CourseId}/pdfs";
                    await _storageService.DeleteFileAsync(bucketDir, pdf.FileName);
                    _logger.LogInformation("Deleted PDF from cloud bucket: {BucketDir}/{FileName}", bucketDir, pdf.FileName);
                }
                catch (Exception cloudEx)
                {
                    _logger.LogError(cloudEx, "Failed to delete PDF from cloud bucket: {FileName}", pdf.FileName);
                }
            }

            _context.CoursePdfs.Remove(pdf);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted PDF record ID: {PdfId} for Course ID: {CourseId}", requestedId, pdf.CourseId);
            return Json(new { success = true, message = "PDF deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while deleting PDF ID: {PdfId}", requestedId);
            return Json(new { success = false, message = "Delete failed" });
        }
    }

    [Permission("Courses.View")]
    public async Task<IActionResult> Preview(int id)
    {
        var pdf = await _context.CoursePdfs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PdfId == id);

        if (pdf == null)
        {
            _logger.LogWarning("Preview failed. PDF ID {PdfId} not found.", id);
            TempData["ErrorMessage"] = "PDF not found";
            return RedirectToAction("Index", "Courses");
        }

        return RedirectToAction("ViewPdf", "Courses", new { area = "User", pdfId = id });
    }

    [Permission("Courses.View")]
    public async Task<IActionResult> Stream(int id)
    {
        _logger.LogInformation("Streaming requested for PDF ID: {PdfId} by User: {Username}", id, User.Identity?.Name ?? "Unknown User");

        var pdf = await _context.CoursePdfs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PdfId == id);

        if (pdf == null)
        {
            _logger.LogWarning("Stream failed. PDF record with ID {PdfId} not found.", id);
            return NotFound();
        }

        var pdfPath = _pdfLocator.Resolve(pdf.CourseId, pdf.FileName);

        if (pdfPath == null)
        {
            _logger.LogError("Stream failed. Physical file does not exist on server. File: {FileName}, PDF ID: {PdfId}", pdf.FileName, id);
            return NotFound();
        }

        Response.Headers["Content-Disposition"] = "inline";
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        _logger.LogInformation("Successfully initiating stream for PDF ID: {PdfId}", id);
        return PhysicalFile(pdfPath, "application/pdf");
    }
}