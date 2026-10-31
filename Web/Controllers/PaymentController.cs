using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.DTOs.Payment;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Web.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ILemonSqueezyService _lemonSqueezyService;
        private readonly LemonSqueezySettings _lemonSettings;
        private readonly IApplicationDbContext _context;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ILemonSqueezyService lemonSqueezyService,
            Microsoft.Extensions.Options.IOptions<LemonSqueezySettings> lemonSettings,
            IApplicationDbContext context,
            ILogger<PaymentController> logger)
        {
            _lemonSqueezyService = lemonSqueezyService;
            _lemonSettings = lemonSettings.Value;
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int courseId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
            {
                TempData["ErrorMessage"] = "Course not found.";
                return RedirectToAction("Index", "Courses");
            }

            if (!course.IsListedForSale)
            {
                TempData["ErrorMessage"] = "This course is not available for individual purchase.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            var isEnrolled = await _context.CourseEnrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
            if (isEnrolled)
            {
                TempData["InfoMessage"] = "You are already enrolled in this course.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            // Prices are in OMR; the other currencies in the picker are display estimates only.
            ViewBag.Course = course;
            ViewBag.DetectedCurrency = CurrencyHelper.SiteCurrency;
            ViewBag.Currencies = CurrencyHelper.Currencies.Values.OrderBy(c => c.Code == CurrencyHelper.SiteCurrency ? 0 : 1).ToList();

            // What the payment provider will actually charge (OMR, or the USD equivalent).
            var chargeCurrency = _lemonSettings.IsSupportedCurrency ? _lemonSettings.Currency.ToUpperInvariant() : "USD";
            ViewBag.ChargeCurrency = chargeCurrency;
            ViewBag.ChargeAmount = CurrencyHelper.FromSiteCurrency(course.EffectivePrice, chargeCurrency);

            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int courseId, string selectedCurrency)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
            {
                TempData["ErrorMessage"] = "Course not found.";
                return RedirectToAction("Index", "Courses");
            }

            if (!course.IsListedForSale || !course.IsActive)
            {
                TempData["ErrorMessage"] = "This course is not available for individual purchase.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            // Checkout only checks this when the page is shown; check again so nobody pays twice.
            if (await _context.CourseEnrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId))
            {
                TempData["InfoMessage"] = "You are already enrolled in this course.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            // The currency picker on the checkout page only shows an estimate: the charge is always in USD.
            var returnUrl = Url.Action("LemonSqueezyCallback", "Payment", new { courseId }, Request.Scheme);
            if (string.IsNullOrEmpty(returnUrl))
            {
                TempData["ErrorMessage"] = "Error generating payment return URL.";
                return RedirectToAction("Checkout", new { courseId });
            }

            var approvalUrl = await _lemonSqueezyService.CreateCheckoutAsync(userId, courseId, course.EffectivePrice, returnUrl);
            if (string.IsNullOrEmpty(approvalUrl))
            {
                TempData["ErrorMessage"] = "The payment gateway is unavailable right now. Please try again later.";
                return RedirectToAction("Checkout", new { courseId });
            }

            return Redirect(approvalUrl);
        }

        [HttpGet]
        public async Task<IActionResult> DemoLemonSqueezyPayment(string token, int courseId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var transaction = await _context.PaymentTransactions
                .Include(t => t.Course)
                .FirstOrDefaultAsync(t => t.OrderToken == token && t.UserId == userId && t.CourseId == courseId);

            if (transaction == null || !transaction.Status.Equals("Pending", System.StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Transaction is invalid or has already expired.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            return View("DemoLemonSqueezyPayment", transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDemoPayment(string token, int courseId)
        {
            var success = await _lemonSqueezyService.CompleteDemoOrderAsync(token, GetCurrentUserId());
            if (success)
            {
                TempData["SuccessMessage"] = "Demo payment completed (no money was charged). Your course enrollment is now active.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            TempData["ErrorMessage"] = "Could not complete demo payment.";
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }

        /// <summary>
        /// Where Lemon Squeezy sends the student after paying. Access is granted by the webhook, which may
        /// arrive a few seconds later, so only claim success once the enrollment actually exists.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LemonSqueezyCallback(int courseId)
        {
            var userId = GetCurrentUserId();
            if (await _context.CourseEnrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId))
                TempData["SuccessMessage"] = "Payment completed. Your course access is active.";
            else
                TempData["InfoMessage"] = "Thank you! Your payment is being confirmed — your course will unlock within a minute. Refresh this page shortly.";

            return RedirectToAction("Details", "Courses", new { id = courseId });
        }

        [HttpGet]
        public async Task<IActionResult> LemonSqueezyCancel(string? token, int courseId)
        {
            if (!string.IsNullOrEmpty(token))
            {
                await _lemonSqueezyService.CancelOrderAsync(token, GetCurrentUserId());
            }
            TempData["InfoMessage"] = "Checkout was cancelled.";
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> LemonSqueezyWebhook()
        {
            using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            var rawJson = await reader.ReadToEndAsync();
            var signature = Request.Headers["X-Signature"].ToString();

            var success = await _lemonSqueezyService.ProcessWebhookAsync(rawJson, signature);
            return success ? Ok(new { message = "Webhook processed" }) : BadRequest(new { message = "Webhook processing failed" });
        }

        // Old link format for the demo checkout page.
        [HttpGet]
        public async Task<IActionResult> DemoPayment(string token, int courseId) =>
            await DemoLemonSqueezyPayment(token, courseId);
    }
}