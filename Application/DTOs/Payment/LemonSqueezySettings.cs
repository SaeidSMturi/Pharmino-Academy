namespace Application.DTOs.Payment
{
    public class LemonSqueezySettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;

        /// <summary>
        /// The product variant every course checkout is created against (Lemon Squeezy requires one;
        /// the course title and price are overridden per checkout).
        /// </summary>
        public string VariantId { get; set; } = string.Empty;

        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>
        /// "Live", "Test", or "Demo"
        /// "Demo": Simulated instant checkout for local and staging development. No money moves.
        /// "Test": Calls Lemon Squeezy in test mode.
        /// "Live": Real payment processing.
        /// </summary>
        public string Mode { get; set; } = "Demo";

        /// <summary>
        /// The Lemon Squeezy store currency, i.e. what students are charged in: "OMR" or "USD".
        /// Prices on the site are always OMR; with "USD" the exact USD equivalent (at the peg) is charged.
        /// </summary>
        public string Currency { get; set; } = "USD";

        public bool IsSupportedCurrency =>
            string.Equals(Currency, "OMR", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Currency, "USD", System.StringComparison.OrdinalIgnoreCase);

        public bool IsDemo => string.Equals(Mode, "Demo", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Names of the settings a real (Live/Test) checkout still needs; empty when ready.</summary>
        public System.Collections.Generic.List<string> MissingForRealPayments()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (IsDemo) return missing;
            if (string.IsNullOrWhiteSpace(ApiKey)) missing.Add("ApiKey");
            if (string.IsNullOrWhiteSpace(StoreId)) missing.Add("StoreId");
            if (string.IsNullOrWhiteSpace(VariantId)) missing.Add("VariantId");
            if (string.IsNullOrWhiteSpace(WebhookSecret)) missing.Add("WebhookSecret");
            if (!IsSupportedCurrency) missing.Add("Currency (must be OMR or USD)");
            return missing;
        }
    }
}
