using System;
using System.Collections.Generic;

namespace Application.DTOs.Payment
{
    public class CurrencyInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal RateToUsd { get; set; }
    }

    public static class CurrencyHelper
    {
        #region Site currency (Omani Rial)

        /// <summary>All course and PDF prices are stored and shown in this currency.</summary>
        public const string SiteCurrency = "OMR";

        /// <summary>
        /// Fixed rate used to convert between OMR and USD (the rial is pegged to the dollar).
        /// Used for the one-time price conversion and when the payment provider charges in USD.
        /// </summary>
        public const decimal OmrPerUsd = 0.385m;

        /// <summary>Currencies whose smallest unit is 1/1000 (e.g. 1 OMR = 1000 baisa).</summary>
        private static readonly HashSet<string> ThreeDecimalCurrencies =
            new(StringComparer.OrdinalIgnoreCase) { "OMR", "KWD", "BHD", "JOD", "TND", "LYD", "IQD" };

        public static int DecimalDigits(string currency) => ThreeDecimalCurrencies.Contains(currency) ? 3 : 2;

        /// <summary>Amount in the currency's smallest unit (cents, baisa…), as payment APIs expect.</summary>
        public static long ToMinorUnits(decimal amount, string currency)
        {
            var factor = DecimalDigits(currency) == 3 ? 1000m : 100m;
            return (long)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
        }

        /// <summary>Converts a site-currency (OMR) amount to the currency the payment provider charges in.</summary>
        public static decimal FromSiteCurrency(decimal amountInOmr, string targetCurrency)
        {
            if (string.Equals(targetCurrency, SiteCurrency, StringComparison.OrdinalIgnoreCase))
                return Math.Round(amountInOmr, 3, MidpointRounding.AwayFromZero);
            if (string.Equals(targetCurrency, "USD", StringComparison.OrdinalIgnoreCase))
                return Math.Round(amountInOmr / OmrPerUsd, 2, MidpointRounding.AwayFromZero);
            throw new NotSupportedException($"Charging in {targetCurrency} is not supported; use OMR or USD.");
        }

        /// <summary>Converts an amount charged by the payment provider back to OMR.</summary>
        public static decimal ToSiteCurrency(decimal amount, string fromCurrency)
        {
            if (string.Equals(fromCurrency, SiteCurrency, StringComparison.OrdinalIgnoreCase))
                return amount;
            if (string.Equals(fromCurrency, "USD", StringComparison.OrdinalIgnoreCase))
                return Math.Round(amount * OmrPerUsd, 3, MidpointRounding.AwayFromZero);

            // Older transactions were recorded in the student's local currency: approximate via the rate table.
            if (Currencies.TryGetValue(fromCurrency, out var info) && info.RateToUsd > 0)
                return Math.Round(amount / info.RateToUsd * OmrPerUsd, 3, MidpointRounding.AwayFromZero);
            return amount;
        }

        /// <summary>"19.250 OMR"; whole amounts drop the decimals ("20 OMR").</summary>
        public static string Format(decimal amount, string currency = SiteCurrency)
        {
            var digits = DecimalDigits(currency);
            var text = amount == Math.Round(amount)
                ? amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                : amount.ToString("N" + digits, System.Globalization.CultureInfo.InvariantCulture);
            return $"{text} {currency.ToUpperInvariant()}";
        }

        /// <summary>
        /// Estimate of an OMR amount in another currency, for display only (Currencies holds units per 1 USD).
        /// </summary>
        public static decimal EstimateFromSiteCurrency(decimal amountInOmr, string targetCurrencyCode)
        {
            var usd = amountInOmr / OmrPerUsd;
            return ConvertFromUsd(usd, targetCurrencyCode);
        }

        #endregion

        public static readonly Dictionary<string, CurrencyInfo> Currencies = new(StringComparer.OrdinalIgnoreCase)
{
    { "USD", new CurrencyInfo { Code = "USD", Name = "US Dollar", Symbol = "$", RateToUsd = 1.0m } },
    { "SAR", new CurrencyInfo { Code = "SAR", Name = "Saudi Riyal", Symbol = "SR", RateToUsd = 3.75m } },
    { "AED", new CurrencyInfo { Code = "AED", Name = "UAE Dirham", Symbol = "AED", RateToUsd = 3.67m } },
    { "EGP", new CurrencyInfo { Code = "EGP", Name = "Egyptian Pound", Symbol = "EGP", RateToUsd = 48.0m } },
    { "KWD", new CurrencyInfo { Code = "KWD", Name = "Kuwaiti Dinar", Symbol = "KD", RateToUsd = 0.31m } },
    { "QAR", new CurrencyInfo { Code = "QAR", Name = "Qatari Riyal", Symbol = "QR", RateToUsd = 3.64m } },
    { "OMR", new CurrencyInfo { Code = "OMR", Name = "Omani Rial", Symbol = "OMR", RateToUsd = 0.385m } },
    { "BHD", new CurrencyInfo { Code = "BHD", Name = "Bahraini Dinar", Symbol = "BD", RateToUsd = 0.38m } },
    { "JOD", new CurrencyInfo { Code = "JOD", Name = "Jordanian Dinar", Symbol = "JD", RateToUsd = 0.71m } },
    { "IQD", new CurrencyInfo { Code = "IQD", Name = "Iraqi Dinar", Symbol = "ID", RateToUsd = 1310.0m } },
    { "LBP", new CurrencyInfo { Code = "LBP", Name = "Lebanese Pound", Symbol = "LL", RateToUsd = 89500.0m } },
    { "YER", new CurrencyInfo { Code = "YER", Name = "Yemeni Rial", Symbol = "YR", RateToUsd = 250.0m } },
    { "LYD", new CurrencyInfo { Code = "LYD", Name = "Libyan Dinar", Symbol = "LD", RateToUsd = 4.80m } },
    { "SDG", new CurrencyInfo { Code = "SDG", Name = "Sudanese Pound", Symbol = "SDG", RateToUsd = 601.0m } },
    { "MAD", new CurrencyInfo { Code = "MAD", Name = "Moroccan Dirham", Symbol = "DH", RateToUsd = 10.0m } },
    { "DZD", new CurrencyInfo { Code = "DZD", Name = "Algerian Dinar", Symbol = "DA", RateToUsd = 134.0m } },
    { "TND", new CurrencyInfo { Code = "TND", Name = "Tunisian Dinar", Symbol = "DT", RateToUsd = 3.10m } },
    { "SYP", new CurrencyInfo { Code = "SYP", Name = "Syrian Pound", Symbol = "LS", RateToUsd = 13000.0m } }
};

        public static string AutoSelectCurrency(string? acceptLanguage)
        {
            if (string.IsNullOrEmpty(acceptLanguage))
                return "USD";

            var parts = acceptLanguage.Split(',');
            foreach (var part in parts)
            {
                var locale = part.Split(';')[0].Trim().ToLower();
                if (locale.StartsWith("ar-") || locale == "ar")
                {
                    if (locale.Length >= 5)
                    {
                        var country = locale.Substring(3, 2);
                        switch (country)
                        {
                            case "sa": return "SAR";
                            case "ae": return "AED";
                            case "eg": return "EGP";
                            case "kw": return "KWD";
                            case "qa": return "QAR";
                            case "om": return "OMR";
                            case "bh": return "BHD";
                            case "jo": return "JOD";
                            case "iq": return "IQD";
                            case "lb": return "LBP";
                            case "ye": return "YER";
                            case "ly": return "LYD";
                            case "sd": return "SDG";
                            case "ma": return "MAD";
                            case "dz": return "DZD";
                            case "tn": return "TND";
                            case "sy": return "SYP";
                        }
                    }
                    return "SAR"; // Default Arab currency (Saudi Riyal)
                }
            }
            return "USD";
        }

        public static decimal ConvertFromUsd(decimal amountInUsd, string targetCurrencyCode)
        {
            if (Currencies.TryGetValue(targetCurrencyCode, out var currencyInfo))
            {
                return amountInUsd * currencyInfo.RateToUsd;
            }
            return amountInUsd;
        }
    }
}
