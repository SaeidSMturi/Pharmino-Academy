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
        public static readonly Dictionary<string, CurrencyInfo> Currencies = new(StringComparer.OrdinalIgnoreCase)
{
    { "USD", new CurrencyInfo { Code = "USD", Name = "US Dollar", Symbol = "$", RateToUsd = 1.0m } },
    { "SAR", new CurrencyInfo { Code = "SAR", Name = "Saudi Riyal", Symbol = "SR", RateToUsd = 3.75m } },
    { "AED", new CurrencyInfo { Code = "AED", Name = "UAE Dirham", Symbol = "AED", RateToUsd = 3.67m } },
    { "EGP", new CurrencyInfo { Code = "EGP", Name = "Egyptian Pound", Symbol = "EGP", RateToUsd = 48.0m } },
    { "KWD", new CurrencyInfo { Code = "KWD", Name = "Kuwaiti Dinar", Symbol = "KD", RateToUsd = 0.31m } },
    { "QAR", new CurrencyInfo { Code = "QAR", Name = "Qatari Riyal", Symbol = "QR", RateToUsd = 3.64m } },
    { "OMR", new CurrencyInfo { Code = "OMR", Name = "Omani Rial", Symbol = "OR", RateToUsd = 0.38m } },
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
