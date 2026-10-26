using System.Threading.Tasks;
using Domain.Entities.Course;

namespace Application.Interfaces
{
    public interface ILemonSqueezyService
    {
        /// <summary>
        /// Creates a payment transaction and generates a checkout URL (hosted Lemon Squeezy checkout or internal Demo checkout).
        /// </summary>
        Task<string?> CreateCheckoutAsync(int userId, int courseId, decimal localAmount, string currency, decimal amountInUsd, string returnUrl, string cancelUrl);

        /// <summary>
        /// Validates HMAC-SHA256 signature and processes a Lemon Squeezy webhook payload (order_created, order_refunded).
        /// </summary>
        Task<bool> ProcessWebhookAsync(string rawJson, string? signatureHeader);

        /// <summary>
        /// Simulates successful order completion for Demo mode and activates course enrollment.
        /// </summary>
        Task<bool> CompleteDemoOrderAsync(string orderToken);

        /// <summary>
        /// Marks a pending order as cancelled.
        /// </summary>
        Task<bool> CancelOrderAsync(string orderToken);
    }
}
