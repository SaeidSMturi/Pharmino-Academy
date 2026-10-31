using System;
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

        public async Task<string?> CreateCheckoutAsync(int userId, int courseId, decimal priceInOmr, string returnUrl)
        {
            if (!_settings.IsSupportedCurrency)
            {
                _logger.LogError("LemonSqueezy:Currency is {Currency}; it must be OMR or USD. Checkout refused.", _settings.Currency);
                return null;
            }

            // Outside Demo mode, refuse to start a checkout that cannot work. Without the webhook secret the
            // student would be charged but never enrolled, so that one is required too.
            var missing = _settings.MissingForRealPayments();
            if (missing.Count > 0)
            {
                _logger.LogError("Lemon Squeezy is in {Mode} mode but {Missing} is not set. Checkout refused.", _settings.Mode, string.Join(", ", missing));
                return null;
            }

            var orderToken = "LS-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();

            // Record what the student is actually charged: the OMR price, or its exact USD equivalent
            // when the store charges in dollars.
            var chargeCurrency = _settings.Currency.ToUpperInvariant();
            var transaction = new PaymentTransaction
            {
                UserId = userId,
                CourseId = courseId,
                OrderToken = orderToken,
                Amount = CurrencyHelper.FromSiteCurrency(priceInOmr, chargeCurrency),
                Currency = chargeCurrency,
                Status = "Pending",
                CreateDate = DateTime.UtcNow,
                Gateway = "LemonSqueezy",
                Mode = _settings.IsDemo ? "Demo" : _settings.Mode
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            if (_settings.IsDemo)
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

                var payload = new
                {
                    data = new
                    {
                        type = "checkouts",
                        attributes = new
                        {
                            custom_price = CurrencyHelper.ToMinorUnits(transaction.Amount, transaction.Currency),
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
                            store = new { data = new { type = "stores", id = _settings.StoreId } },
                            variant = new { data = new { type = "variants", id = _settings.VariantId } }
                        }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/vnd.api+json");
                var response = await client.PostAsync("https://api.lemonsqueezy.com/v1/checkouts", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                        dataEl.TryGetProperty("attributes", out var attrEl) &&
                        attrEl.TryGetProperty("url", out var urlEl) &&
                        !string.IsNullOrEmpty(urlEl.GetString()))
                    {
                        return urlEl.GetString();
                    }
                }

                _logger.LogError("Lemon Squeezy checkout API error ({Status}) for order {OrderToken}: {Response}", response.StatusCode, orderToken, responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling Lemon Squeezy API for order {OrderToken}.", orderToken);
            }

            // Never fall back to the demo checkout here: that would hand out courses without payment.
            transaction.Status = "Failed";
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return null;
        }

        public async Task<bool> ProcessWebhookAsync(string rawJson, string? signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                _logger.LogWarning("Lemon Squeezy webhook payload is empty.");
                return false;
            }

            // Fail closed: without a configured secret nothing proves the request came from Lemon Squeezy.
            if (string.IsNullOrWhiteSpace(_settings.WebhookSecret))
            {
                _logger.LogError("Lemon Squeezy webhook rejected: LemonSqueezy:WebhookSecret is not configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(signatureHeader) || !VerifySignature(rawJson, signatureHeader, _settings.WebhookSecret))
            {
                _logger.LogWarning("Lemon Squeezy webhook signature verification failed.");
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("meta", out var metaEl) || !root.TryGetProperty("data", out var dataEl) ||
                    !dataEl.TryGetProperty("attributes", out var attrs))
                {
                    _logger.LogWarning("Lemon Squeezy webhook has no meta/data.");
                    return false;
                }

                var eventName = GetString(metaEl, "event_name");
                var orderId = GetString(dataEl, "id") ?? "";
                _logger.LogInformation("Lemon Squeezy webhook event {EventName} for order {OrderId}.", eventName, orderId);

                // Only orders from our own store, in the mode we are running in.
                if (GetString(attrs, "store_id") != _settings.StoreId)
                {
                    _logger.LogWarning("Lemon Squeezy webhook for order {OrderId} is from another store; ignored.", orderId);
                    return true;
                }

                var isTestOrder = attrs.TryGetProperty("test_mode", out var tm) && tm.ValueKind == JsonValueKind.True;
                var expectTest = !string.Equals(_settings.Mode, "Live", StringComparison.OrdinalIgnoreCase);
                if (isTestOrder != expectTest)
                {
                    _logger.LogWarning("Lemon Squeezy webhook for order {OrderId} has test_mode={TestMode} while running in {Mode}; ignored.", orderId, isTestOrder, _settings.Mode);
                    return true;
                }

                if (eventName == "order_created")
                    await HandleOrderCreatedAsync(metaEl, attrs, orderId);
                else if (eventName == "order_refunded")
                    await HandleOrderRefundedAsync(orderId);

                // Acknowledge everything else so Lemon Squeezy does not keep retrying.
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Lemon Squeezy webhook.");
                return false;
            }
        }

        private async Task HandleOrderCreatedAsync(JsonElement meta, JsonElement attrs, string orderId)
        {
            string? orderToken = null;
            if (meta.TryGetProperty("custom_data", out var customEl))
                orderToken = GetString(customEl, "order_token");

            if (string.IsNullOrEmpty(orderToken))
            {
                _logger.LogWarning("Lemon Squeezy order {OrderId} has no order_token; ignored.", orderId);
                return;
            }

            var transaction = await _context.PaymentTransactions.FirstOrDefaultAsync(t => t.OrderToken == orderToken);
            if (transaction == null)
            {
                _logger.LogWarning("Lemon Squeezy order {OrderId} refers to unknown order token {OrderToken}.", orderId, orderToken);
                return;
            }

            // Webhooks are retried: process each order once.
            if (!string.Equals(transaction.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Order {OrderToken} is already {Status}; webhook ignored.", orderToken, transaction.Status);
                return;
            }

            var status = GetString(attrs, "status");
            if (!string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Lemon Squeezy order {OrderId} has status {Status}; not enrolling.", orderId, status);
                return;
            }

            // The amount and currency charged must match what this order was created for.
            var currency = GetString(attrs, "currency");
            var total = attrs.TryGetProperty("total", out var totalEl) && totalEl.TryGetInt64(out var cents) ? cents : -1;
            var expected = CurrencyHelper.ToMinorUnits(transaction.Amount, transaction.Currency);
            if (!string.Equals(currency, transaction.Currency, StringComparison.OrdinalIgnoreCase) || total != expected)
            {
                transaction.Status = "Failed";
                transaction.PaymentId = orderId;
                transaction.UpdateDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogError("Lemon Squeezy order {OrderId} paid {Total} {Currency} (minor units) but order {OrderToken} expects {Expected} {ExpectedCurrency}. Marked Failed for review.",
                    orderId, total, currency, orderToken, expected, transaction.Currency);
                return;
            }

            transaction.Status = "Completed";
            transaction.PaymentId = orderId;
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId,
                CurrencyHelper.ToSiteCurrency(transaction.Amount, transaction.Currency));
            _logger.LogInformation("User {UserId} enrolled in course {CourseId} via Lemon Squeezy order {OrderId}.", transaction.UserId, transaction.CourseId, orderId);
        }

        private async Task HandleOrderRefundedAsync(string orderId)
        {
            var transaction = await _context.PaymentTransactions.FirstOrDefaultAsync(t => t.PaymentId == orderId);
            if (transaction == null || string.Equals(transaction.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
                return;

            transaction.Status = "Refunded";
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _courseService.RevokeEnrollmentAsync(transaction.UserId, transaction.CourseId);
            _logger.LogInformation("Transaction {TransactionId} refunded via webhook; enrollment of user {UserId} in course {CourseId} revoked.",
                transaction.TransactionId, transaction.UserId, transaction.CourseId);
        }

        public async Task<bool> CompleteDemoOrderAsync(string orderToken, int userId)
        {
            // Demo completion only exists while the site runs in Demo mode, and only for the student's own demo order.
            if (!_settings.IsDemo)
            {
                _logger.LogWarning("Demo payment confirmation attempted for {OrderToken} while in {Mode} mode; refused.", orderToken, _settings.Mode);
                return false;
            }

            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderToken == orderToken && t.UserId == userId && t.Status == "Pending" && t.Mode == "Demo");

            if (transaction == null) return false;

            transaction.Status = "Completed";
            transaction.PaymentId = "DEMO-LS-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId,
                CurrencyHelper.ToSiteCurrency(transaction.Amount, transaction.Currency));
            _logger.LogInformation("Demo Lemon Squeezy transaction {OrderToken} completed for course {CourseId}.", orderToken, transaction.CourseId);
            return true;
        }

        public async Task<bool> CancelOrderAsync(string orderToken, int userId)
        {
            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderToken == orderToken && t.UserId == userId && t.Status == "Pending");

            if (transaction == null) return false;

            transaction.Status = "Cancelled";
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>Reads a property as a string whether the JSON holds it as a string or a number.</summary>
        private static string? GetString(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.GetRawText(),
                _ => null
            };
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
