using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IPayPalService
    {
        Task<string?> CreateOrderAsync(int userId, int courseId, decimal localAmount, string currency, decimal amountInUsd, string returnUrl, string cancelUrl);
        Task<bool> CaptureOrderAsync(string orderToken);
        Task<bool> CancelOrderAsync(string orderToken);
    }
}
