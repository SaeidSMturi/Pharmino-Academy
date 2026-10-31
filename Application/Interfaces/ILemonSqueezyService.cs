using System.Threading.Tasks;
using Domain.Entities.Course;

namespace Application.Interfaces
{
    public interface ILemonSqueezyService
    {
        /// <summary>
        /// Creates a pending payment transaction and returns the checkout URL: the hosted Lemon Squeezy
        /// checkout, or the internal demo checkout in Demo mode. Returns null if the checkout could not be created.
        /// The price is in OMR; the transaction records what is actually charged (store currency: OMR, or USD equivalent).
        /// </summary>
        Task<string?> CreateCheckoutAsync(int userId, int courseId, decimal priceInOmr, string returnUrl);

        /// <summary>
        /// Verifies the HMAC-SHA256 signature and processes a Lemon Squeezy webhook (order_created, order_refunded).
        /// Returns false only for requests that should be rejected (bad signature, unreadable payload).
        /// </summary>
        Task<bool> ProcessWebhookAsync(string rawJson, string? signatureHeader);

        /// <summary>
        /// Demo mode only: completes the user's own pending demo order and activates the enrollment.
        /// </summary>
        Task<bool> CompleteDemoOrderAsync(string orderToken, int userId);

        /// <summary>
        /// Marks the user's own pending order as cancelled.
        /// </summary>
        Task<bool> CancelOrderAsync(string orderToken, int userId);
    }
}
