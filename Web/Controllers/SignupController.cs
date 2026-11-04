using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Application.DTOs.Account;
using Application.Interfaces;
using Application.Services;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Services.Signup;

namespace Web.Controllers
{
    /// <summary>
    /// JSON endpoints behind the one-page sign-up (Views/Account/Register.cshtml). Steps 1–5 only check
    /// things; nothing is stored until <see cref="Create"/>, which requires a token proving the email was
    /// verified with the emailed code.
    /// </summary>
    [AllowAnonymous]
    [Route("Signup/[action]")]
    public class SignupController : Controller
    {
        private readonly IUserRepository _users;
        private readonly IAccountService _accounts;
        private readonly IEmailSender _email;
        private readonly SignupVerificationService _verification;
        private readonly ILogger<SignupController> _logger;

        public SignupController(IUserRepository users, IAccountService accounts, IEmailSender email,
            SignupVerificationService verification, ILogger<SignupController> logger)
        {
            _users = users;
            _accounts = accounts;
            _email = email;
            _verification = verification;
            _logger = logger;
        }

        private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static readonly Regex EmailShape = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled);
        // Letters in any script, spaces, apostrophes, hyphens and dots.
        private static readonly Regex NameShape = new(@"^[\p{L}\p{M}][\p{L}\p{M}' .\-]{0,199}$", RegexOptions.Compiled);

        private JsonResult Fail(string message, object? extra = null) => Json(new { ok = false, message, extra });
        private JsonResult Slow() => Json(new { ok = false, message = "Too many attempts. Please wait a few minutes and try again." });

        public sealed record EmailRequest(string? Email);
        public sealed record PhoneRequest(string? Phone, string? Region);
        public sealed record SendCodeRequest(string? Email, string? FirstName);
        public sealed record VerifyRequest(string? Email, string? Code);

        // Step 1: is the email usable and still free?
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckEmail([FromBody] EmailRequest req)
        {
            if (!_verification.Allow("signup:check:" + Ip, 40, TimeSpan.FromMinutes(10))) return Slow();
            var email = req.Email?.Trim() ?? "";
            if (email.Length > 200 || !EmailShape.IsMatch(email)) return Fail("Enter a valid email address");
            if (await _users.IsEmailExistsAsync(email)) return Fail("This email is already registered. Sign in instead.");
            return Json(new { ok = true });
        }

        // Step 4: is the phone number valid for its country and still free?
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckPhone([FromBody] PhoneRequest req)
        {
            if (!_verification.Allow("signup:check:" + Ip, 40, TimeSpan.FromMinutes(10))) return Slow();
            if (!PhoneNumberValidator.TryNormalize(req.Phone, req.Region, out var e164))
                return Fail("Enter a valid mobile number for the selected country");
            if (await _users.IsPhoneNumberExistsAsync(e164)) return Fail("This phone number is already registered");
            return Json(new { ok = true, e164 });
        }

        // After step 5: email a code. Resending replaces the previous code.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SendCode([FromBody] SendCodeRequest req)
        {
            var email = req.Email?.Trim() ?? "";
            if (!EmailShape.IsMatch(email)) return Fail("Enter a valid email address");
            if (await _users.IsEmailExistsAsync(email)) return Fail("This email is already registered. Sign in instead.");

            var result = _verification.TryCreateCode(email, Ip, out var code);
            if (result.Status == SendCodeStatus.TooSoon)
                return Json(new { ok = false, retryAfter = result.RetryAfterSeconds, message = $"Please wait {result.RetryAfterSeconds} seconds before asking for another code." });
            if (result.Status == SendCodeStatus.TooMany)
                return Fail("Too many codes requested. Please try again in an hour.");

            var sent = await _email.SendAsync(email, $"Your Pharmino code: {code}", CodeEmailHtml(req.FirstName, code), CodeEmailText(req.FirstName, code));
            if (!sent)
            {
                _logger.LogError("Sign-up code email to {Email} could not be sent.", email);
                return Fail("We couldn't send the email right now. Please try again in a minute.");
            }
            return Json(new { ok = true, resendIn = SignupVerificationService.ResendSeconds, length = SignupVerificationService.CodeLength });
        }

        // Step 6: check the code. A correct code returns the token Create needs.
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult VerifyCode([FromBody] VerifyRequest req)
        {
            if (!_verification.Allow("signup:verify:" + Ip, 30, TimeSpan.FromMinutes(10))) return Slow();
            var r = _verification.Verify(req.Email ?? "", (req.Code ?? "").Trim());
            return r.Status switch
            {
                VerifyStatus.Verified => Json(new { ok = true, token = r.Token }),
                VerifyStatus.Wrong => Json(new { ok = false, triesLeft = r.TriesLeft, message = $"Invalid code. {r.TriesLeft} {(r.TriesLeft == 1 ? "try" : "tries")} left." }),
                VerifyStatus.Locked => Json(new { ok = false, locked = true, message = "Too many wrong codes. Request a new one." }),
                _ => Json(new { ok = false, expired = true, message = "This code has expired. Request a new one." })
            };
        }

        // The only step that writes anything: re-check everything, then create the account.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] SignupDto model)
        {
            if (!ModelState.IsValid)
            {
                var first = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                return Fail(first ?? "Some details are missing or invalid. Please go back and check them.");
            }

            var first_ = model.FirstName.Trim();
            var last = model.LastName.Trim();
            if (!NameShape.IsMatch(first_) || !NameShape.IsMatch(last)) return Fail("Enter your first and last name using letters only");

            if (!DateTime.TryParseExact(model.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
                return Fail("Enter a valid date of birth");
            var today = DateTime.UtcNow.Date;
            if (dob > today.AddYears(-SignupTerms.MinimumAge)) return Fail($"You must be at least {SignupTerms.MinimumAge} years old to register");
            if (dob < today.AddYears(-100)) return Fail("Enter a valid date of birth");

            if (!PhoneNumberValidator.TryNormalize(model.PhoneNumber, model.PhoneRegion, out var phone))
                return Fail("Enter a valid mobile number for the selected country");

            if (model.TermsVersion != SignupTerms.Version) return Fail("The terms have changed. Please reload the page and review them again.");
            if (!model.AcceptTerms || !model.AcceptCompensation || !model.AcceptDataProcessing)
                return Fail("Please accept the terms, the compensation clause and the data processing consent");
            static string Squash(string s) => Regex.Replace(s.Trim(), @"\s+", " ");
            if (!string.Equals(Squash(model.Signature), Squash(first_ + " " + last), StringComparison.OrdinalIgnoreCase))
                return Fail("Your signature must match your first and last name");

            // Last: this uses the token up, so only consume it once everything else is valid.
            if (!_verification.TryConsumeToken(model.VerificationToken, model.Email))
                return Fail("Your email verification has expired. Please request a new code.");

            var result = await _accounts.CreateVerifiedAccountAsync(model, phone, dob, Ip);
            if (!result.Success) return Fail(result.Message);

            _logger.LogInformation("New account {UserId} created via verified sign-up (terms v{Version}).", result.UserId, model.TermsVersion);
            TempData["SuccessMessage"] = "Your account is ready. Sign in to start learning.";
            return Json(new { ok = true, redirect = Url.Action("Login", "Account") });
        }

        private static string CodeEmailText(string? name, string code) =>
            $"Hi {name},\n\nYour Pharmino verification code is: {code}\n\nIt expires in 10 minutes. If you didn't try to create an account, you can ignore this email.\n\nPharmino Academy";

        private static string CodeEmailHtml(string? name, string code)
        {
            var who = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? "there" : name.Trim());
            var digits = string.Concat(code.Select(c =>
                $"<td style=\"width:46px;height:56px;border:2px solid #e2e8f0;border-radius:10px;text-align:center;font:700 26px Arial,sans-serif;color:#0f172a\">{c}</td><td style=\"width:8px\"></td>"));
            return $@"<div style=""background:#f4f6fb;padding:32px 12px;font-family:Arial,sans-serif"">
<table role=""presentation"" style=""max-width:440px;margin:0 auto;background:#ffffff;border-radius:16px;padding:28px"" width=""100%"">
<tr><td style=""text-align:center"">
<div style=""display:inline-block;width:56px;height:24px;border-radius:12px;background:linear-gradient(90deg,#4272FF 50%,#FF7E42 50%)""></div>
<h1 style=""font-size:20px;color:#0f172a;margin:16px 0 6px"">Verify your email</h1>
<p style=""font-size:14px;color:#475569;margin:0 0 20px"">Hi {who}, enter this code on the Pharmino sign-up page:</p>
<table role=""presentation"" style=""margin:0 auto""><tr>{digits}</tr></table>
<p style=""font-size:13px;color:#64748b;margin:20px 0 0"">The code expires in 10 minutes. If you didn't try to create an account, you can ignore this email.</p>
</td></tr></table></div>";
        }
    }
}
