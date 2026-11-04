using PhoneNumbers;

namespace Application.Services
{
    /// <summary>
    /// Phone number checks with Google's libphonenumber (the same rules libphonenumber-js applies in the
    /// browser), so the server agrees with what the sign-up page showed.
    /// </summary>
    public static class PhoneNumberValidator
    {
        private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

        /// <summary>
        /// Parses a number typed for <paramref name="regionCode"/> (ISO 3166-1 alpha-2, e.g. "OM"), or a full
        /// "+…" number. Returns the E.164 form ("+96891234567") when it is a valid mobile or fixed-line number.
        /// </summary>
        public static bool TryNormalize(string? raw, string? regionCode, out string e164)
        {
            e164 = string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            try
            {
                var number = Util.Parse(raw, string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.ToUpperInvariant());
                if (!Util.IsValidNumber(number)) return false;

                var type = Util.GetNumberType(number);
                if (type is not (PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE or PhoneNumberType.FIXED_LINE))
                    return false;

                e164 = Util.Format(number, PhoneNumberFormat.E164);
                return true;
            }
            catch (NumberParseException)
            {
                return false;
            }
        }
    }
}
