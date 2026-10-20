namespace Application.DTOs.Payment
{
    public class PayPalSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Mode { get; set; } = "Demo"; // "Live", "Sandbox", "Demo"
        public string Currency { get; set; } = "USD";
    }
}
