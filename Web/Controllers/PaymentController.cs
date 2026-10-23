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
        private readonly IPayPalService _payPalService;
        private readonly IApplicationDbContext _context;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPayPalService payPalService,
            IApplicationDbContext context,
            ILogger<PaymentController> logger)
        {
            _payPalService = payPalService;
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

            var isEnrolled = await _context.CourseEnrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
            if (isEnrolled)
            {
                TempData["InfoMessage"] = "You are already enrolled in this course.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            // Detect user language for auto currency selection
            var acceptLanguage = Request.Headers["Accept-Language"].ToString();
            var detectedCurrency = CurrencyHelper.AutoSelectCurrency(acceptLanguage);

            ViewBag.Course = course;
            ViewBag.DetectedCurrency = detectedCurrency;
            ViewBag.Currencies = CurrencyHelper.Currencies.Values.ToList();

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

            if (string.IsNullOrEmpty(selectedCurrency) || !CurrencyHelper.Currencies.ContainsKey(selectedCurrency))
            {
                selectedCurrency = "USD";
            }

            var effectivePrice = course.EffectivePrice;
            var localAmount = CurrencyHelper.ConvertFromUsd(effectivePrice, selectedCurrency);

            var returnUrl = Url.Action("PayPalCallback", "Payment", new { courseId }, Request.Scheme);
            var cancelUrl = Url.Action("PayPalCancel", "Payment", new { courseId }, Request.Scheme);

            if (string.IsNullOrEmpty(returnUrl) || string.IsNullOrEmpty(cancelUrl))
            {
                TempData["ErrorMessage"] = "Error generating payment return URL.";
                return RedirectToAction("Checkout", new { courseId });
            }

            var approvalUrl = await _payPalService.CreateOrderAsync(userId, courseId, localAmount, selectedCurrency, effectivePrice, returnUrl, cancelUrl);
            if (string.IsNullOrEmpty(approvalUrl))
            {
                TempData["ErrorMessage"] = "Error connecting to the payment gateway.";
                return RedirectToAction("Checkout", new { courseId });
            }

            return Redirect(approvalUrl);
        }

        [HttpGet]
        public async Task<IActionResult> DemoPayment(string token, int courseId)
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

            return View(transaction);
        }

        [HttpGet]
        public async Task<IActionResult> PayPalCallback(string token, int courseId)
        {
            _logger.LogInformation("PayPalCallback received token {Token} for course {CourseId}.", token, courseId);

            var success = await _payPalService.CaptureOrderAsync(token);
            if (success)
            {
                TempData["SuccessMessage"] = "Payment completed successfully. Your access to the course has been activated.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            TempData["ErrorMessage"] = "Payment failed or an error occurred.";
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }

        [HttpGet]
        public async Task<IActionResult> PayPalCancel(string token, int courseId)
        {
            _logger.LogWarning("PayPalCancel received token {Token} for course {CourseId}.", token, courseId);

            await _payPalService.CancelOrderAsync(token);
            TempData["InfoMessage"] = "Payment process was canceled by the user.";
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }
    }
}