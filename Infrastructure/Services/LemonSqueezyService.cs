using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Application.DTOs.Payment;
using Application.Interfaces;
using Domain.Entities.Course;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class LemonSqueezyService : ILemonSqueezyService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICourseService _courseService;
        private readonly LemonSqueezySettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LemonSqueezyService> _logger;

        public LemonSqueezyService(
            IApplicationDbContext context,
            ICourseService courseService,
            IOptions<LemonSqueezySettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<LemonSqueezyService> logger)
        {
            _context = context;
            _courseService = courseService;
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> CreateCheckoutAsync(
            int userId,
            int courseId,
            decimal localAmount,
            string currency,
            decimal amountInUsd,
            string returnUrl,
            string cancelUrl)
        {
            var orderToken = "LS-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                CourseId = courseId,
                OrderToken = orderToken,
                Amount = localAmount,
                Currency = currency,
                Status = "Pending",
                CreateDate = DateTime.UtcNow,
                Gateway = "LemonSqueezy",
                Mode = _settings.Mode
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            // Demo Mode fallback or unconfigured credentials
            if (string.Equals(_settings.Mode, "Demo", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(_settings.ApiKey) ||
                string.IsNullOrWhiteSpace(_settings.StoreId))
            {
                _logger.LogInformation("LemonSqueezy is in Demo mode. Using demo checkout for order {OrderToken}.", orderToken);
                return $"/Payment/DemoLemonSqueezyPayment?token={orderToken}&courseId={courseId}";
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                var course = await _context.Courses.FindAsync(courseId);

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

                var cents = (int)Math.Round(amountInUsd * 100, MidpointRounding.AwayFromZero);

                var payload = new
                {
                    data = new
                    {
                        type = "checkouts",
                        attributes = new
                        {
                            custom_price = cents,
                            product_options = new
                            {
                                name = course?.Title ?? "Course Enrollment",
                                description = course?.Description ?? "Pharmino Academy Course",
                                redirect_url = returnUrl
                            },
                            checkout_data = new
                            {
                                email = user?.Email ?? "",
                                name = $"{user?.FirstName} {user?.LastName}".Trim(),
                                custom = new
                                {
                                    order_token = orderToken,
                                    user_id = userId.ToString(),
                                    course_id = courseId.ToString()
                                }
                            },
                            test_mode = string.Equals(_settings.Mode, "Test", StringComparison.OrdinalIgnoreCase)
                        },
                        relationships = new
                        {
                            store = new
                            {
                                data = new
                                {
                                    type = "stores",
                                    id = _settings.StoreId
                                }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/vnd.api+json");

                var response = await client.PostAsync("https://api.lemonsqueezy.com/v1/checkouts", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                        dataEl.TryGetProperty("attributes", out var attrEl) &&
                        attrEl.TryGetProperty("url", out var urlEl))
                    {
                        var hostedUrl = urlEl.GetString();
                        if (!string.IsNullOrEmpty(hostedUrl))
                        {
                            _logger.LogInformation("Lemon Squeezy checkout created: {Url}", hostedUrl);
                            return hostedUrl;
                        }
                    }
                }

                _logger.LogWarning("Lemon Squeezy checkout API error ({Status}): {Response}. Falling back to demo checkout.", response.StatusCode, responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling Lemon Squeezy API. Falling back to demo checkout.");
            }

            return $"/Payment/DemoLemonSqueezyPayment?token={orderToken}&courseId={courseId}";
        }

        public async Task<bool> ProcessWebhookAsync(string rawJson, string? signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                _logger.LogWarning("Lemon Squeezy webhook payload is empty.");
                return false;
            }

            // Verify Webhook Signature if secret configured
            if (!string.IsNullOrWhiteSpace(_settings.WebhookSecret))
            {
                if (string.IsNullOrWhiteSpace(signatureHeader) || !VerifySignature(rawJson, signatureHeader, _settings.WebhookSecret))
                {
                    _logger.LogWarning("Lemon Squeezy webhook signature verification failed.");
                    return false;
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                var eventName = string.Empty;
                if (root.TryGetProperty("meta", out var metaEl) &&
                    metaEl.TryGetProperty("event_name", out var eventEl))
                {
                    eventName = eventEl.GetString();
                }

                _logger.LogInformation("Lemon Squeezy webhook event received: {EventName}", eventName);

                if (eventName == "order_created")
                {
                    string? orderToken = null;
                    if (metaEl.TryGetProperty("custom_data", out var customEl))
                    {
                        if (customEl.TryGetProperty("order_token", out var tokenProp))
                            orderToken = tokenProp.GetString();
                    }

                    var orderId = string.Empty;
                    if (root.TryGetProperty("data", out var dataEl))
                    {
                        if (dataEl.TryGetProperty("id", out var idProp))
                            orderId = idProp.GetString() ?? "";
                    }

                    PaymentTransaction? transaction = null;
                    if (!string.IsNullOrEmpty(orderToken))
                    {
                        transaction = await _context.PaymentTransactions
                            .FirstOrDefaultAsync(t => t.OrderToken == orderToken);
                    }

                    if (transaction != null)
                    {
                        transaction.Status = "Completed";
                        transaction.PaymentId = orderId;
                        transaction.UpdateDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        // Enroll the user in the purchased course
                        await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId, transaction.Amount);
                        _logger.LogInformation("User {UserId} enrolled in course {CourseId} via Lemon Squeezy webhook.", transaction.UserId, transaction.CourseId);
                        return true;
                    }
                }
                else if (eventName == "order_refunded")
                {
                    var orderId = string.Empty;
                    if (root.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("id", out var idProp))
                        orderId = idProp.GetString() ?? "";

                    var transaction = await _context.PaymentTransactions
                        .FirstOrDefaultAsync(t => t.PaymentId == orderId);

                    if (transaction != null)
                    {
                        transaction.Status = "Refunded";
                        transaction.UpdateDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Transaction {TransactionId} marked as Refunded via webhook.", transaction.TransactionId);
                        return true;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Lemon Squeezy webhook.");
                return false;
            }
        }

        public async Task<bool> CompleteDemoOrderAsync(string orderToken)
        {
            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderToken == orderToken && t.Status == "Pending");

            if (transaction == null) return false;

            transaction.Status = "Completed";
            transaction.PaymentId = "DEMO-LS-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId, transaction.Amount);
            _logger.LogInformation("Demo Lemon Squeezy transaction {OrderToken} completed for course {CourseId}.", orderToken, transaction.CourseId);
            return true;
        }

        public async Task<bool> CancelOrderAsync(string orderToken)
        {
            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderToken == orderToken && t.Status == "Pending");

            if (transaction == null) return false;

            transaction.Status = "Cancelled";
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static bool VerifySignature(string payload, string signatureHeader, string secret)
        {
            try
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var expected = Convert.ToHexString(hash).ToLowerInvariant();
                var provided = signatureHeader.Trim().ToLowerInvariant();

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(provided));
            }
            catch
            {
                return false;
            }
        }
    }
}
