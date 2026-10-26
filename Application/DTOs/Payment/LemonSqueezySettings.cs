namespace Application.DTOs.Payment
{
    public class LemonSqueezySettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>
        /// "Live", "Test", or "Demo"
        /// "Demo": Simulated instant checkout for local and staging development.
        /// "Test": Calls Lemon Squeezy sandbox.
        /// "Live": Real payment processing.
        /// </summary>
        public string Mode { get; set; } = "Demo";

        public string Currency { get; set; } = "USD";
    }
}
