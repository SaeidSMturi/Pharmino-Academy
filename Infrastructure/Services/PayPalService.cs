using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Application.DTOs.Payment;
using Application.Interfaces;
using Domain.Entities.Course;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class PayPalService : IPayPalService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICourseService _courseService;
        private readonly PayPalSettings _settings;

        public PayPalService(
            IApplicationDbContext context,
            ICourseService courseService,
            IOptions<PayPalSettings> settings)
        {
            _context = context;
            _courseService = courseService;
            _settings = settings.Value;
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            var url = _settings.Mode.Equals("Live", StringComparison.OrdinalIgnoreCase)
                ? "https://api-m.paypal.com/v1/oauth2/token"
                : "https://api-m.sandbox.paypal.com/v1/oauth2/token";

            using var client = new HttpClient();
            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

            var requestData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            };

            var response = await client.PostAsync(url, new FormUrlEncodedContent(requestData));
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.GetProperty("access_token").GetString();
        }

        public async Task<string?> CreateOrderAsync(int userId, int courseId, decimal localAmount, string currency, decimal amountInUsd, string returnUrl, string cancelUrl)
        {
            var orderToken = "PAY-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                CourseId = courseId,
                OrderToken = orderToken,
                Amount = localAmount,
                Currency = currency,
                Status = "Pending",
                CreateDate = DateTime.UtcNow,
                Gateway = "PayPal",
                Mode = _settings.Mode
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            if (_settings.Mode.Equals("Demo", StringComparison.OrdinalIgnoreCase))
            {
                // For Demo mode, redirect to our demo payment page
                return $"/Payment/DemoPayment?token={orderToken}&courseId={courseId}";
            }

            var payPalAmount = localAmount;
            var payPalCurrency = currency;
            if (!IsPayPalSupportedCurrency(currency))
            {
                // PayPal does not support this currency, so charge in USD
                payPalAmount = amountInUsd;
                payPalCurrency = "USD";
            }

            var accessToken = await GetAccessTokenAsync();
            if (accessToken == null)
            {
                transaction.Status = "Failed";
                await _context.SaveChangesAsync();
                return null;
            }

            var baseUrl = _settings.Mode.Equals("Live", StringComparison.OrdinalIgnoreCase) 
                ? "https://api-m.paypal.com" 
                : "https://api-m.sandbox.paypal.com";
            var url = $"{baseUrl}/v2/checkout/orders";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var requestBody = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        amount = new
                        {
                            currency_code = payPalCurrency,
                            value = payPalAmount.ToString("F2", CultureInfo.InvariantCulture)
                        },
                        description = $"Registration for course ID {courseId} (Local: {localAmount} {currency})"
                    }
                },
                application_context = new
                {
                    return_url = returnUrl,
                    cancel_url = cancelUrl
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var response = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                transaction.Status = "Failed";
                await _context.SaveChangesAsync();
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var paypalOrderId = root.GetProperty("id").GetString();
            if (string.IsNullOrEmpty(paypalOrderId))
                return null;

            // Update order token to the actual PayPal Order ID in database
            transaction.OrderToken = paypalOrderId;
            await _context.SaveChangesAsync();

            // Find the approval link
            var links = root.GetProperty("links");
            for (int i = 0; i < links.GetArrayLength(); i++)
            {
                var rel = links[i].GetProperty("rel").GetString();
                if (rel.Equals("approve", StringComparison.OrdinalIgnoreCase))
                {
                    return links[i].GetProperty("href").GetString();
                }
            }

            return null;
        }

        public async Task<bool> CaptureOrderAsync(string orderToken)
        {
            var transaction = await _context.PaymentTransactions
                .Include(t => t.Course)
                .FirstOrDefaultAsync(t => t.OrderToken == orderToken);

            if (transaction == null || !transaction.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                return false;

            if (transaction.Mode.Equals("Demo", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Status = "Completed";
                transaction.PaymentId = "PAY-CAPTURED-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
                transaction.UpdateDate = DateTime.UtcNow;

                var enrolled = await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId, transaction.Amount);
                if (enrolled)
                {
                    await _context.SaveChangesAsync();
                    return true;
                }

                transaction.Status = "Failed";
                await _context.SaveChangesAsync();
                return false;
            }

            var accessToken = await GetAccessTokenAsync();
            if (accessToken == null)
            {
                transaction.Status = "Failed";
                await _context.SaveChangesAsync();
                return false;
            }

            var baseUrl = transaction.Mode.Equals("Live", StringComparison.OrdinalIgnoreCase) 
                ? "https://api-m.paypal.com" 
                : "https://api-m.sandbox.paypal.com";
            var url = $"{baseUrl}/v2/checkout/orders/{orderToken}/capture";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await client.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                transaction.Status = "Failed";
                await _context.SaveChangesAsync();
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var status = root.GetProperty("status").GetString();

            if (status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Status = "Completed";

                try
                {
                    var purchaseUnits = root.GetProperty("purchase_units");
                    if (purchaseUnits.GetArrayLength() > 0)
                    {
                        var payments = purchaseUnits[0].GetProperty("payments");
                        var captures = payments.GetProperty("captures");
                        if (captures.GetArrayLength() > 0)
                        {
                            transaction.PaymentId = captures[0].GetProperty("id").GetString();
                        }
                    }
                }
                catch
                {
                    transaction.PaymentId = "UNKNOWN";
                }

                transaction.UpdateDate = DateTime.UtcNow;

                var enrolled = await _courseService.EnrollUserInCourseAsync(transaction.UserId, transaction.CourseId, transaction.Amount);
                if (enrolled)
                {
                    await _context.SaveChangesAsync();
                    return true;
                }
            }

            transaction.Status = "Failed";
            await _context.SaveChangesAsync();
            return false;
        }

        public async Task<bool> CancelOrderAsync(string orderToken)
        {
            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderToken == orderToken);

            if (transaction == null || !transaction.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                return false;

            transaction.Status = "Cancelled";
            transaction.UpdateDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private bool IsPayPalSupportedCurrency(string currency)
        {
            var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "USD", "EUR", "GBP", "CAD", "AUD", "JPY", "ILS", "SGD", "HKD", "MXN", "PLN", "SEK", "CHF", "THB", "NOK", "DKK"
            };
            return supported.Contains(currency);
        }
    }
}
