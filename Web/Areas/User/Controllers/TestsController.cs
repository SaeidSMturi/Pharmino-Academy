using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities.Exam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class TestsController : Controller
    {
        /// <summary>Extra time accepted after the deadline, for slow networks and the auto-submit round trip.</summary>
        private static readonly TimeSpan SubmitGrace = TimeSpan.FromSeconds(30);

        private readonly IApplicationDbContext _context;
        private readonly ICourseService _courseService;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<TestsController> _logger;

        public TestsController(
            IApplicationDbContext context,
            ICourseService courseService,
            IPermissionService permissionService,
            ILogger<TestsController> logger)
        {
            _context = context;
            _courseService = courseService;
            _permissionService = permissionService;
            _logger = logger;
        }

        /// <summary>A question as shown in one attempt (questions/options possibly shuffled).</summary>
        public sealed record TakeQuestion(ExamQuestion Question, List<QuestionOption> Options);

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var uid) ? uid : 0;
        }

        /// <summary>
        /// Tests not linked to a course are open to every signed-in user; course tests need an enrollment
        /// (direct or through a bundle). Staff who can view courses can open any test to check it.
        /// </summary>
        private async Task<bool> CanAccessExamAsync(int userId, Exam exam)
        {
            if (exam.CourseId == null) return true;
            if (await _courseService.IsUserEnrolledAsync(userId, exam.CourseId.Value)) return true;
            return await _permissionService.UserHasPermissionAsync(userId, "Courses.View");
        }

        private static DateTime? Deadline(ExamAttempt attempt, Exam exam) =>
            exam.DurationMinutes > 0 ? attempt.StartTime.AddMinutes(exam.DurationMinutes) : null;

        private static bool IsPastDeadline(ExamAttempt attempt, Exam exam) =>
            Deadline(attempt, exam) is DateTime deadline && DateTime.UtcNow > deadline + SubmitGrace;

        // GET: /User/Tests
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();

            var activeExams = await _context.Exams
                .Where(e => e.IsActive)
                .Include(e => e.Course)
                .Include(e => e.Questions)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            var exams = new List<Exam>();
            foreach (var exam in activeExams)
            {
                if (await CanAccessExamAsync(userId, exam)) exams.Add(exam);
            }

            var userAttempts = await _context.ExamAttempts
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            ViewBag.UserAttempts = userAttempts;
            return View(exams);
        }

        // GET: /User/Tests/Take/5
        public async Task<IActionResult> Take(int id)
        {
            var userId = GetCurrentUserId();

            var exam = await _context.Exams
                .Include(e => e.Course)
                .Include(e => e.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.IsActive);

            if (exam == null || !await CanAccessExamAsync(userId, exam))
            {
                TempData["ErrorMessage"] = "Test not found or currently unavailable.";
                return RedirectToAction(nameof(Index));
            }

            // Create or resume attempt
            var attempt = await _context.ExamAttempts
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.ExamId == id && a.UserId == userId && !a.IsCompleted);

            // An unfinished attempt whose time ran out (e.g. the tab was closed) is graded as it stands.
            if (attempt != null && IsPastDeadline(attempt, exam))
            {
                attempt.Exam = exam;
                Grade(attempt);
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = "Your previous attempt ran out of time and has been submitted.";
                return RedirectToAction(nameof(Result), new { attemptId = attempt.AttemptId });
            }

            if (attempt == null)
            {
                attempt = new ExamAttempt
                {
                    ExamId = id,
                    UserId = userId,
                    StartTime = DateTime.UtcNow,
                    TotalPoints = exam.Questions.Sum(q => q.Points),
                    IsCompleted = false
                };
                _context.ExamAttempts.Add(attempt);
                await _context.SaveChangesAsync();
            }

            ViewBag.Attempt = attempt;
            ViewBag.RemainingSeconds = Deadline(attempt, exam) is DateTime deadline
                ? Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalSeconds)
                : -1; // no time limit
            ViewBag.Questions = BuildDisplayOrder(exam, attempt.AttemptId);

            return View(exam);
        }

        /// <summary>
        /// Orders questions and options for one attempt. Shuffling is seeded by the attempt, so a page reload
        /// shows the same order while different attempts get different orders.
        /// </summary>
        private static List<TakeQuestion> BuildDisplayOrder(Exam exam, int attemptId)
        {
            var questions = exam.Questions.OrderBy(q => q.OrderIndex).ToList();
            if (exam.ShuffleQuestions)
            {
                var rng = new Random(attemptId);
                questions = questions.OrderBy(_ => rng.Next()).ToList();
            }

            return questions.Select(q =>
            {
                var options = q.Options.OrderBy(o => o.OptionLabel).ToList();
                if (exam.ShuffleOptions)
                {
                    var rng = new Random(HashCode.Combine(attemptId, q.QuestionId));
                    options = options.OrderBy(_ => rng.Next()).ToList();
                }
                return new TakeQuestion(q, options);
            }).ToList();
        }

        public class SubmitAnswerRequest
        {
            public int AttemptId { get; set; }
            public int QuestionId { get; set; }
            public int OptionId { get; set; }
        }

        // POST: /User/Tests/SubmitAnswer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer([FromBody] SubmitAnswerRequest req)
        {
            var userId = GetCurrentUserId();

            var attempt = await _context.ExamAttempts
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.AttemptId == req.AttemptId && a.UserId == userId && !a.IsCompleted);

            if (attempt == null)
                return Json(new { success = false, message = "Attempt session not found or already completed." });

            if (IsPastDeadline(attempt, attempt.Exam))
                return Json(new { success = false, expired = true, message = "Time is up. Your test is being submitted." });

            // The option must belong to the question, and the question to this attempt's exam.
            var option = await _context.QuestionOptions
                .FirstOrDefaultAsync(o => o.OptionId == req.OptionId &&
                                          o.QuestionId == req.QuestionId &&
                                          o.Question.ExamId == attempt.ExamId);

            if (option == null)
                return Json(new { success = false, message = "Invalid option." });

            var existingAnswer = await _context.AttemptAnswers
                .FirstOrDefaultAsync(a => a.AttemptId == req.AttemptId && a.QuestionId == req.QuestionId);

            if (existingAnswer == null)
            {
                existingAnswer = new AttemptAnswer
                {
                    AttemptId = req.AttemptId,
                    QuestionId = req.QuestionId,
                    SelectedOptionId = req.OptionId,
                    IsCorrect = option.IsCorrect
                };
                _context.AttemptAnswers.Add(existingAnswer);
            }
            else
            {
                existingAnswer.SelectedOptionId = req.OptionId;
                existingAnswer.IsCorrect = option.IsCorrect;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public class FinishExamRequest
        {
            public int AttemptId { get; set; }
        }

        // POST: /User/Tests/FinishExam
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinishExam([FromBody] FinishExamRequest req)
        {
            var userId = GetCurrentUserId();

            var attempt = await _context.ExamAttempts
                .Include(a => a.Exam)
                .ThenInclude(e => e.Questions)
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.AttemptId == req.AttemptId && a.UserId == userId && !a.IsCompleted);

            if (attempt == null)
                return Json(new { success = false, message = "Attempt not found or already completed." });

            // Answers after the deadline were already refused by SubmitAnswer, so grading what is saved is fair.
            Grade(attempt);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                scorePercent = attempt.ScorePercent,
                isPassed = attempt.IsPassed,
                redirectUrl = Url.Action(nameof(Result), new { attemptId = attempt.AttemptId })
            });
        }

        /// <summary>Scores the attempt from its saved answers and marks it completed. Needs Exam.Questions and Answers loaded.</summary>
        private static void Grade(ExamAttempt attempt)
        {
            var deadline = Deadline(attempt, attempt.Exam);
            attempt.EndTime = deadline is DateTime d && DateTime.UtcNow > d ? d : DateTime.UtcNow;
            attempt.IsCompleted = true;

            decimal totalEarned = 0;
            decimal totalPossible = 0;

            foreach (var q in attempt.Exam.Questions)
            {
                totalPossible += q.Points;
                var ans = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.QuestionId);
                if (ans != null && ans.IsCorrect)
                {
                    totalEarned += q.Points;
                }
            }

            attempt.Score = totalEarned;
            attempt.TotalPoints = totalPossible > 0 ? totalPossible : 1;
            attempt.ScorePercent = Math.Round((totalEarned / attempt.TotalPoints) * 100, 2);
            attempt.IsPassed = attempt.ScorePercent >= attempt.Exam.PassingScorePercent;
        }

        // GET: /User/Tests/Result?attemptId=5
        public async Task<IActionResult> Result(int attemptId)
        {
            var userId = GetCurrentUserId();

            var attempt = await _context.ExamAttempts
                .Include(a => a.Exam)
                .ThenInclude(e => e.Questions)
                .ThenInclude(q => q.Options)
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId && a.UserId == userId);

            if (attempt == null) return NotFound();

            // Results (with correct answers) only for finished attempts.
            if (!attempt.IsCompleted)
                return RedirectToAction(nameof(Take), new { id = attempt.ExamId });

            return View(attempt);
        }
    }
}
