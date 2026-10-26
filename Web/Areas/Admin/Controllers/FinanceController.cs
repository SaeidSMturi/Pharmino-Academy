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

            // Overall Financial KPIs (Unfiltered)
            var completedTransactions = await allTransactionsQuery
                .Where(t => t.Status == "Completed")
                .ToListAsync();

            var totalRevenue = completedTransactions.Sum(t => t.Amount);
            var completedCount = completedTransactions.Count;

            var pendingCount = await allTransactionsQuery.CountAsync(t => t.Status == "Pending");
            var refundedList = await allTransactionsQuery.Where(t => t.Status == "Refunded").ToListAsync();
            var refundedCount = refundedList.Count;
            var totalRefundedAmount = refundedList.Sum(t => t.Amount);

            // Top Courses Revenue
            var topCourses = completedTransactions
                .Where(t => t.Course != null)
                .GroupBy(t => new { t.CourseId, t.Course.Title })
                .Select(g => new CourseRevenueStat
                {
                    CourseId = g.Key.CourseId,
                    CourseTitle = g.Key.Title,
                    CompletedSalesCount = g.Count(),
                    TotalRevenue = g.Sum(x => x.Amount)
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
                LemonSqueezyMode = _lemonSettings.Mode,
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
            var validStatuses = new[] { "Completed", "Pending", "Refunded", "Failed", "Cancelled" };
            if (!validStatuses.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Invalid status specified." });

            var transaction = await _context.PaymentTransactions
                .Include(t => t.Course)
                .FirstOrDefaultAsync(t => t.TransactionId == id);

            if (transaction == null) return Json(new { success = false, message = "Transaction not found." });

            var oldStatus = transaction.Status;
            transaction.Status = newStatus;
            transaction.UpdateDate = DateTime.UtcNow;

            // If changing to Completed manually, ensure enrollment is granted
            if (string.Equals(newStatus, "Completed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(oldStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId, transaction.Amount);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin updated transaction {TransactionId} status from {Old} to {New}", id, oldStatus, newStatus);

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
                var name = $"{t.User?.FirstName} {t.User?.LastName}".Trim().Replace("\"", "\"\"");
                var course = (t.Course?.Title ?? "").Replace("\"", "\"\"");
                sb.AppendLine($"{t.TransactionId},\"{t.OrderToken}\",\"{t.PaymentId}\",\"{name}\",\"{t.User?.Email}\",\"{course}\",{t.Amount},{t.Currency},{t.Gateway},{t.Mode},{t.Status},{t.CreateDate:yyyy-MM-dd HH:mm:ss}");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"pharmino-transactions-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }
    }
}
