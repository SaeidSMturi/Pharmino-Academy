using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.DTOs.Payment;
using Application.Interfaces;
using Domain.Entities.Course;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Web.Areas.Admin.ViewModels;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class FinanceController : AdminBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly ICourseService _courseService;
        private readonly LemonSqueezySettings _lemonSettings;
        private readonly ILogger<FinanceController> _logger;

        private const int PageSize = 20;

        public FinanceController(
            IApplicationDbContext context,
            ICourseService courseService,
            IOptions<LemonSqueezySettings> lemonSettings,
            ILogger<FinanceController> logger)
        {
            _context = context;
            _courseService = courseService;
            _lemonSettings = lemonSettings.Value;
            _logger = logger;
        }

        // GET: /Admin/Finance
        [Permission("Payments.View")]
        public async Task<IActionResult> Index(
            string? searchQuery,
            string? status,
            string? gateway,
            string? dateRange,
            int page = 1)
        {
            if (page < 1) page = 1;

            var allTransactionsQuery = _context.PaymentTransactions
                .Include(t => t.User)
                .Include(t => t.Course)
                .AsNoTracking();

            // Overall Financial KPIs (Unfiltered). Totals are reported in OMR and only count real money:
            // demo orders move none. Payments charged in OMR or USD are converted at the peg; older records
            // in other currencies are listed separately rather than guessed at.
            var reportingCurrency = CurrencyHelper.SiteCurrency;
            var realMoney = _context.PaymentTransactions.AsNoTracking()
                .Where(t => t.Mode != "Demo" && (t.Currency == "OMR" || t.Currency == "USD"));

            // Summed per course, status and currency in the database; converted to OMR here.
            var sums = await realMoney
                .Where(t => t.Status == "Completed" || t.Status == "Refunded")
                .GroupBy(t => new { t.CourseId, t.Course.Title, t.Status, t.Currency })
                .Select(g => new { g.Key.CourseId, g.Key.Title, g.Key.Status, g.Key.Currency, Count = g.Count(), Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            decimal InOmr(decimal amount, string currency) => CurrencyHelper.ToSiteCurrency(amount, currency);

            var completedSums = sums.Where(s => s.Status == "Completed").ToList();
            var refundedSums = sums.Where(s => s.Status == "Refunded").ToList();
            var totalRevenue = completedSums.Sum(s => InOmr(s.Total, s.Currency));
            var completedCount = completedSums.Sum(s => s.Count);
            var refundedCount = refundedSums.Sum(s => s.Count);
            var totalRefundedAmount = refundedSums.Sum(s => InOmr(s.Total, s.Currency));
            var pendingCount = await allTransactionsQuery.CountAsync(t => t.Status == "Pending");
            var demoCompletedCount = await _context.PaymentTransactions.CountAsync(t => t.Mode == "Demo" && t.Status == "Completed");
            var otherCurrencyCount = await _context.PaymentTransactions.CountAsync(t => t.Mode != "Demo" && t.Status == "Completed" && t.Currency != "OMR" && t.Currency != "USD");

            // Top Courses Revenue
            var topCourses = completedSums
                .GroupBy(s => new { s.CourseId, s.Title })
                .Select(g => new CourseRevenueStat
                {
                    CourseId = g.Key.CourseId,
                    CourseTitle = g.Key.Title,
                    CompletedSalesCount = g.Sum(s => s.Count),
                    TotalRevenue = g.Sum(s => InOmr(s.Total, s.Currency))
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(5)
                .ToList();

            // Filtered query for the Ledger Table
            var query = allTransactionsQuery;

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();
                query = query.Where(t =>
                    t.OrderToken.ToLower().Contains(q) ||
                    (t.PaymentId != null && t.PaymentId.ToLower().Contains(q)) ||
                    t.User.UserName.ToLower().Contains(q) ||
                    (t.User.Email != null && t.User.Email.ToLower().Contains(q)) ||
                    (t.User.FirstName != null && t.User.FirstName.ToLower().Contains(q)) ||
                    (t.User.LastName != null && t.User.LastName.ToLower().Contains(q)) ||
                    t.Course.Title.ToLower().Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(gateway) && !gateway.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.Gateway.ToLower() == gateway.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(dateRange))
            {
                var now = DateTime.UtcNow;
                query = dateRange.ToLower() switch
                {
                    "today" => query.Where(t => t.CreateDate >= now.Date),
                    "7days" => query.Where(t => t.CreateDate >= now.AddDays(-7)),
                    "30days" => query.Where(t => t.CreateDate >= now.AddDays(-30)),
                    "thismonth" => query.Where(t => t.CreateDate.Month == now.Month && t.CreateDate.Year == now.Year),
                    _ => query
                };
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / PageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var items = await query
                .OrderByDescending(t => t.CreateDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var webhookUrl = $"{Request.Scheme}://{Request.Host}/Payment/LemonSqueezyWebhook";

            var model = new FinanceDashboardViewModel
            {
                TotalRevenue = totalRevenue,
                CompletedOrdersCount = completedCount,
                PendingOrdersCount = pendingCount,
                RefundedOrdersCount = refundedCount,
                TotalRefundedAmount = totalRefundedAmount,
                ReportingCurrency = reportingCurrency,
                DemoCompletedCount = demoCompletedCount,
                OtherCurrencyCompletedCount = otherCurrencyCount,
                LemonSqueezyMode = _lemonSettings.Mode,
                MissingPaymentSettings = _lemonSettings.MissingForRealPayments(),
                LemonSqueezyStoreId = _lemonSettings.StoreId,
                WebhookUrl = webhookUrl,
                TopCoursesRevenue = topCourses,
                Transactions = items,
                SearchQuery = searchQuery,
                StatusFilter = status,
                GatewayFilter = gateway,
                DateRangeFilter = dateRange,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = PageSize,
                TotalCount = totalCount
            };

            return View(model);
        }

        // GET: /Admin/Finance/Details/5
        [Permission("Payments.View")]
        public async Task<IActionResult> Details(int id)
        {
            var t = await _context.PaymentTransactions
                .Include(x => x.User)
                .Include(x => x.Course)
                .FirstOrDefaultAsync(x => x.TransactionId == id);

            if (t == null) return NotFound(new { success = false, message = "Transaction not found." });

            return Json(new
            {
                success = true,
                transactionId = t.TransactionId,
                orderToken = t.OrderToken,
                paymentId = t.PaymentId ?? "-",
                amount = t.Amount,
                currency = t.Currency,
                status = t.Status,
                gateway = t.Gateway,
                mode = t.Mode,
                createDate = t.CreateDate.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                updateDate = t.UpdateDate?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "-",
                studentName = $"{t.User?.FirstName} {t.User?.LastName}".Trim(),
                studentEmail = t.User?.Email ?? "-",
                studentUsername = t.User?.UserName ?? "-",
                courseTitle = t.Course?.Title ?? "-"
            });
        }

        // POST: /Admin/Finance/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Payments.Manage")]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            // Only the transitions a person should make by hand; everything else comes from the gateway.
            var allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Pending"] = new[] { "Completed", "Cancelled", "Failed" },
                ["Failed"] = new[] { "Completed" },
                ["Completed"] = new[] { "Refunded" }
            };

            var transaction = await _context.PaymentTransactions
                .Include(t => t.Course)
                .FirstOrDefaultAsync(t => t.TransactionId == id);

            if (transaction == null) return Json(new { success = false, message = "Transaction not found." });

            var oldStatus = transaction.Status;
            if (!allowed.TryGetValue(oldStatus, out var targets) || !targets.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = $"A {oldStatus} transaction can't be changed to {newStatus}." });

            newStatus = targets.First(s => s.Equals(newStatus, StringComparison.OrdinalIgnoreCase));
            transaction.Status = newStatus;
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Keep course access in step with the payment.
            if (newStatus == "Completed")
                await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId,
                    CurrencyHelper.ToSiteCurrency(transaction.Amount, transaction.Currency));
            else if (newStatus == "Refunded")
                await _courseService.RevokeEnrollmentAsync(transaction.UserId, transaction.CourseId);

            _logger.LogWarning("Admin {AdminId} manually changed transaction {TransactionId} ({OrderToken}, user {UserId}, course {CourseId}) from {Old} to {New}.",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, id, transaction.OrderToken, transaction.UserId, transaction.CourseId, oldStatus, newStatus);

            return Json(new { success = true, newStatus = transaction.Status });
        }

        // GET: /Admin/Finance/ExportCsv
        [HttpGet]
        [Permission("Payments.Manage")]
        public async Task<IActionResult> ExportCsv(string? status, string? gateway)
        {
            var query = _context.PaymentTransactions
                .Include(t => t.User)
                .Include(t => t.Course)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(t => t.Status.ToLower() == status.ToLower());

            if (!string.IsNullOrWhiteSpace(gateway) && !gateway.Equals("All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(t => t.Gateway.ToLower() == gateway.ToLower());

            var list = await query.OrderByDescending(t => t.CreateDate).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("TransactionId,OrderToken,PaymentId,StudentName,StudentEmail,Course,Amount,Currency,Gateway,Mode,Status,CreateDate");

            foreach (var t in list)
            {
                var fields = new[]
                {
                    t.TransactionId.ToString(), t.OrderToken, t.PaymentId, $"{t.User?.FirstName} {t.User?.LastName}".Trim(),
                    t.User?.Email, t.Course?.Title, t.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    t.Currency, t.Gateway, t.Mode, t.Status, t.CreateDate.ToString("yyyy-MM-dd HH:mm:ss")
                };
                sb.AppendLine(string.Join(",", fields.Select(CsvField)));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"pharmino-transactions-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        /// <summary>
        /// Quotes a CSV field. Text starting with = + - @ (or tab/CR) is prefixed with ' so Excel shows it
        /// instead of running it as a formula — student names are user-controlled.
        /// </summary>
        private static string CsvField(string? value)
        {
            value ??= string.Empty;
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0]) && !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _))
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
