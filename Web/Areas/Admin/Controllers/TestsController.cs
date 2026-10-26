using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Services;
using Domain.Entities.Exam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class TestsController : AdminBaseController
    {
        private const long MaxPdfBytes = 25 * 1024 * 1024;

        private readonly IApplicationDbContext _context;
        private readonly IPdfExamParserService _parserService;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<TestsController> _logger;

        public TestsController(
            IApplicationDbContext context,
            IPdfExamParserService parserService,
            IPermissionService permissionService,
            ILogger<TestsController> logger)
        {
            _context = context;
            _parserService = parserService;
            _permissionService = permissionService;
            _logger = logger;
        }

        // GET: /Admin/Tests
        [Permission("Courses.View")]
        public async Task<IActionResult> Index()
        {
            var exams = await _context.Exams
                .Include(e => e.Course)
                .Include(e => e.Questions)
                .Include(e => e.Attempts)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return View(exams);
        }

        // GET: /Admin/Tests/Create
        [Permission("Courses.Create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Courses = await _context.Courses
                .Where(c => c.IsActive)
                .OrderBy(c => c.Title)
                .ToListAsync();

            return View();
        }

        // POST: /Admin/Tests/UploadAndParsePdf
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Courses.Create")]
        [RequestSizeLimit(MaxPdfBytes + 1024 * 1024)]
        public IActionResult UploadAndParsePdf(IFormFile? pdfFile, string? rawText)
        {
            try
            {
                ExamParseResult result;

                if (pdfFile != null && pdfFile.Length > 0)
                {
                    if (pdfFile.Length > MaxPdfBytes)
                        return Json(new { success = false, message = "The PDF is larger than 25 MB." });

                    using var stream = new MemoryStream();
                    pdfFile.CopyTo(stream);
                    if (!LooksLikePdf(stream))
                        return Json(new { success = false, message = "This file is not a PDF." });

                    stream.Position = 0;
                    result = _parserService.ParseExamFromPdf(stream);
                }
                else if (!string.IsNullOrWhiteSpace(rawText))
                {
                    result = _parserService.ParseExamFromText(rawText);
                }
                else
                {
                    return Json(new { success = false, message = "Please upload a PDF file or paste exam text." });
                }

                return Json(new { success = true, questions = result.Questions, warnings = result.Warnings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse PDF exam.");
                return Json(new { success = false, message = "The document could not be read. Is it a text-based (not scanned) PDF?" });
            }
        }

        private static bool LooksLikePdf(Stream stream)
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[5];
            return stream.Read(header) == 5 && header.SequenceEqual("%PDF-"u8);
        }

        public class SaveExamRequest
        {
            public int? ExamId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public int? CourseId { get; set; }

            /// <summary>0 = no time limit.</summary>
            public int DurationMinutes { get; set; } = 30;
            public decimal PassingScorePercent { get; set; } = 70.0m;
            public bool IsActive { get; set; } = true;
            public bool ShuffleQuestions { get; set; } = false;
            public bool ShuffleOptions { get; set; } = false;
            public List<ParsedMcqDto> Questions { get; set; } = new();
        }

        // POST: /Admin/Tests/SaveExam
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveExam([FromBody] SaveExamRequest req)
        {
            var isEdit = req.ExamId.HasValue && req.ExamId.Value > 0;
            if (!await HasPermissionAsync(isEdit ? "Courses.Edit" : "Courses.Create"))
                return StatusCode(403, new { success = false, message = "Access denied. You don't have permission to perform this action." });

            if (string.IsNullOrWhiteSpace(req.Title))
                return Json(new { success = false, message = "Exam title is required." });

            if (req.Questions == null || req.Questions.Count == 0)
                return Json(new { success = false, message = "At least one question is required." });

            if (req.DurationMinutes < 0 || req.DurationMinutes > 24 * 60)
                return Json(new { success = false, message = "Duration must be between 0 (no limit) and 1440 minutes." });

            if (req.PassingScorePercent <= 0 || req.PassingScorePercent > 100)
                return Json(new { success = false, message = "Passing score must be between 1 and 100%." });

            for (int i = 0; i < req.Questions.Count; i++)
            {
                var q = req.Questions[i];
                var filled = q.Options.Where(o => !string.IsNullOrWhiteSpace(o.Text)).ToList();
                if (string.IsNullOrWhiteSpace(q.QuestionText))
                    return Json(new { success = false, message = $"Question {i + 1} has no text." });
                if (filled.Count < 2)
                    return Json(new { success = false, message = $"Question {i + 1} needs at least 2 options." });
                if (!filled.Any(o => o.IsCorrect) &&
                    !filled.Any(o => o.Label.Equals(q.DetectedCorrectOption ?? "", StringComparison.OrdinalIgnoreCase)))
                    return Json(new { success = false, message = $"Question {i + 1} has no correct answer selected." });
            }

            try
            {
                Exam? exam;
                if (isEdit)
                {
                    exam = await _context.Exams
                        .Include(e => e.Questions)
                        .ThenInclude(q => q.Options)
                        .FirstOrDefaultAsync(e => e.ExamId == req.ExamId!.Value);

                    if (exam == null)
                        return Json(new { success = false, message = "Exam not found." });

                    // Students' answers point at these questions; replacing them would destroy their results.
                    var attemptCount = await _context.ExamAttempts.CountAsync(a => a.ExamId == exam.ExamId);
                    if (attemptCount > 0)
                        return Json(new
                        {
                            success = false,
                            message = $"This test already has {attemptCount} attempt(s), so its questions can't be replaced. Deactivate it and create a new test instead."
                        });

                    _context.ExamQuestions.RemoveRange(exam.Questions);
                    exam.Questions.Clear();
                }
                else
                {
                    exam = new Exam();
                    _context.Exams.Add(exam);
                }

                exam.Title = req.Title.Trim();
                exam.Description = req.Description;
                exam.CourseId = req.CourseId > 0 ? req.CourseId : null;
                exam.DurationMinutes = req.DurationMinutes;
                exam.PassingScorePercent = req.PassingScorePercent;
                exam.IsActive = req.IsActive;
                exam.ShuffleQuestions = req.ShuffleQuestions;
                exam.ShuffleOptions = req.ShuffleOptions;
                exam.LastUpdated = DateTime.UtcNow;

                int order = 1;
                foreach (var qDto in req.Questions)
                {
                    var q = new ExamQuestion
                    {
                        OrderIndex = order++,
                        QuestionText = qDto.QuestionText.Trim(),
                        Explanation = string.IsNullOrWhiteSpace(qDto.Explanation) ? null : qDto.Explanation.Trim(),
                        Points = 1.0m
                    };

                    foreach (var optDto in qDto.Options)
                    {
                        if (string.IsNullOrWhiteSpace(optDto.Text)) continue;

                        q.Options.Add(new QuestionOption
                        {
                            OptionLabel = optDto.Label.ToUpperInvariant(),
                            OptionText = optDto.Text.Trim(),
                            IsCorrect = optDto.IsCorrect
                        });
                    }

                    // Fallback: if none explicitly marked true but detected option matches
                    if (!q.Options.Any(o => o.IsCorrect) && !string.IsNullOrEmpty(qDto.DetectedCorrectOption))
                    {
                        var target = q.Options.FirstOrDefault(o => o.OptionLabel.Equals(qDto.DetectedCorrectOption, StringComparison.OrdinalIgnoreCase));
                        if (target != null) target.IsCorrect = true;
                    }

                    exam.Questions.Add(q);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, examId = exam.ExamId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save exam.");
                return Json(new { success = false, message = "The test could not be saved. Please try again." });
            }
        }

        // POST: /Admin/Tests/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null) return NotFound();

            exam.IsActive = !exam.IsActive;
            exam.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = exam.IsActive });
        }

        // POST: /Admin/Tests/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Courses.Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _context.Exams.AnyAsync(e => e.ExamId == id)) return NotFound();

            // AttemptAnswers reference questions with ON DELETE RESTRICT, so they must go first.
            // After that, the database cascades Exam → Questions → Options and Exam → Attempts.
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.AttemptAnswers.Where(a => a.Attempt.ExamId == id).ExecuteDeleteAsync();
            await _context.Exams.Where(e => e.ExamId == id).ExecuteDeleteAsync();
            await tx.CommitAsync();

            return Json(new { success = true });
        }

        private async Task<bool> HasPermissionAsync(string permission)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var userId) && await _permissionService.UserHasPermissionAsync(userId, permission);
        }
    }
}
