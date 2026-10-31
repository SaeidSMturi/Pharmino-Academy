using System;
using System.Collections.Generic;
using Domain.Entities.Course;

namespace Web.Areas.Admin.ViewModels
{
    public class FinanceDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int CompletedOrdersCount { get; set; }
        public int PendingOrdersCount { get; set; }
        public int RefundedOrdersCount { get; set; }
        public decimal TotalRefundedAmount { get; set; }

        /// <summary>Currency the money totals are in; only real (non-demo) payments in it are counted.</summary>
        public string ReportingCurrency { get; set; } = "USD";
        public int DemoCompletedCount { get; set; }
        public int OtherCurrencyCompletedCount { get; set; }

        public string LemonSqueezyMode { get; set; } = "Demo";

        /// <summary>Settings still empty for Live/Test payments; students can't pay while any are missing.</summary>
        public List<string> MissingPaymentSettings { get; set; } = new();
        public string LemonSqueezyStoreId { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;

        public List<CourseRevenueStat> TopCoursesRevenue { get; set; } = new();
        public List<PaymentTransaction> Transactions { get; set; } = new();

        // Filters & Pagination
        public string? SearchQuery { get; set; }
        public string? StatusFilter { get; set; }
        public string? GatewayFilter { get; set; }
        public string? DateRangeFilter { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
    }

    public class CourseRevenueStat
    {
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public int CompletedSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
