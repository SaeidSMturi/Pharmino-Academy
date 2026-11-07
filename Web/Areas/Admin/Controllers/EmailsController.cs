using System.Security.Claims;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Domain.Entities.Email;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Attributes;
using Web.Services.Mail;

namespace Web.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin → Emails: create mailboxes on the HestiaCP mail server and manage them (label, password, quota,
    /// aliases, forwarding, suspend, delete). Hestia holds the mailboxes; the Mailboxes table only adds the
    /// Admin / Student label and who created each one.
    /// </summary>
    [Permission("Emails.Manage")]
    public class EmailsController : AdminBaseController
    {
        private static readonly Regex AccountRx = new("^[a-z0-9](?:[a-z0-9._-]{0,62}[a-z0-9])?$", RegexOptions.Compiled);
        private static readonly Regex AddressRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly IMailServerAdmin _mail;
        private readonly WebContext _db;
        private readonly HestiaOptions _options;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<EmailsController> _log;

        public EmailsController(IMailServerAdmin mail, WebContext db, HestiaOptions options, IEmailSender emailSender, ILogger<EmailsController> log)
        {
            _mail = mail;
            _db = db;
            _options = options;
            _emailSender = emailSender;
            _log = log;
        }

        // ---------------------------------------------------------------- pages

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            ViewData["Title"] = "Create Email";
            var (domains, error) = _mail.IsConfigured ? await _mail.ListDomainsAsync(ct) : (Array.Empty<string>(), null);
            return View(new EmailsPageModel(_mail.IsConfigured, _mail.IsDemo, domains, error, _options.MailHost, _options.WebmailUrl));
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "All Emails";
            return View(new EmailsPageModel(_mail.IsConfigured, _mail.IsDemo, Array.Empty<string>(), null, _options.MailHost, _options.WebmailUrl));
        }

        // ---------------------------------------------------------------- data

        /// <summary>Every mailbox on every mail domain, with its label.</summary>
        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            if (!_mail.IsConfigured) return Json(new { success = false, message = "The mail server connection isn't set up yet." });

            var (domains, error) = await _mail.ListDomainsAsync(ct);
            if (error is not null) return Json(new { success = false, message = error });

            var boxes = new List<MailboxInfo>();
            var errors = new List<string>();
            foreach (var d in domains)
            {
                var (list, err) = await _mail.ListAccountsAsync(d, ct);
                if (err is not null) errors.Add(d + ": " + err);
                boxes.AddRange(list);
            }

            var records = await _db.Mailboxes.AsNoTracking().ToDictionaryAsync(m => m.Address, ct);
            var items = boxes
                .Select(b =>
                {
                    records.TryGetValue(b.Address, out var r);
                    // Our own record is UTC; Hestia's date is the server's local time (no zone given).
                    var created = r is not null
                        ? DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc).ToString("o")
                        : b.CreatedAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                    return new
                    {
                        address = b.Address,
                        account = b.Account,
                        domain = b.Domain,
                        label = r?.Label,
                        displayName = r?.DisplayName,
                        createdAt = created,
                        quotaMb = b.QuotaMb,
                        usedMb = b.UsedMb,
                        suspended = b.Suspended,
                        aliases = b.Aliases,
                        forwards = b.Forwards,
                    };
                })
                .OrderByDescending(i => i.createdAt)
                .ToList();

            return Json(new { success = true, items, domains, warnings = errors });
        }

        // ---------------------------------------------------------------- create

        public class CreateMailboxRequest
        {
            public string Account { get; set; } = "";
            public string Domain { get; set; } = "";
            public string Label { get; set; } = "";
            public string? DisplayName { get; set; }
            public string Password { get; set; } = "";
            public int? QuotaMb { get; set; }
            public string? Aliases { get; set; }
            public string? ForwardTo { get; set; }
            public string? SendDetailsTo { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateMailboxRequest req, CancellationToken ct)
        {
            var account = (req.Account ?? "").Trim().ToLowerInvariant();
            var domain = (req.Domain ?? "").Trim().ToLowerInvariant();
            var address = account + "@" + domain;

            if (!MailboxLabels.IsValid(req.Label)) return Fail("Choose whether this mailbox is for an Admin or a Student.", "label");
            if (!AccountRx.IsMatch(account)) return Fail("Use letters, numbers, dots, dashes or underscores (it can't start or end with a symbol).", "account");
            var domainCheck = await KnownDomainAsync(domain, ct);
            if (domainCheck is not null) return Fail(domainCheck, "domain");
            var pwProblem = MailboxPasswordPolicy.Problem(address, req.Password ?? "");
            if (pwProblem is not null) return Fail(pwProblem, "password");
            if (req.QuotaMb is < 1 or > 1_000_000) return Fail("Quota must be between 1 and 1,000,000 MB, or unlimited.", "quota");
            var sendTo = req.SendDetailsTo?.Trim();
            if (!string.IsNullOrEmpty(sendTo) && !AddressRx.IsMatch(sendTo)) return Fail("That isn't a valid email address.", "sendTo");
            var forward = req.ForwardTo?.Trim();
            if (!string.IsNullOrEmpty(forward) && !AddressRx.IsMatch(forward)) return Fail("The forwarding address isn't valid.", "forward");
            var aliases = (req.Aliases ?? "").Split(new[] { ',', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(a => (a.Contains('@') ? a[..a.IndexOf('@')] : a).ToLowerInvariant()).Distinct().ToList();
            if (aliases.Any(a => !AccountRx.IsMatch(a))) return Fail("One of the aliases isn't a valid name.", "aliases");

            var result = await _mail.CreateAsync(domain, account, req.Password!, req.QuotaMb, ct);
            if (!result.Success) return Fail(result.Error!, result.Error!.Contains("taken") ? "account" : null);

            // Extras: report anything that didn't stick, but the mailbox exists either way.
            var warnings = new List<string>();
            foreach (var a in aliases)
            {
                var r = await _mail.AddAliasAsync(domain, account, a, ct);
                if (!r.Success) warnings.Add($"Alias {a}: {r.Error}");
            }
            if (!string.IsNullOrEmpty(forward))
            {
                var r = await _mail.AddForwardAsync(domain, account, forward, ct);
                if (!r.Success) warnings.Add($"Forwarding: {r.Error}");
            }

            var record = await _db.Mailboxes.FirstOrDefaultAsync(m => m.Address == address, ct);
            if (record is null) { record = new Mailbox { Address = address }; _db.Mailboxes.Add(record); }
            record.Label = req.Label;
            record.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? null : req.DisplayName.Trim()[..Math.Min(120, req.DisplayName.Trim().Length)];
            record.CreatedAt = DateTime.UtcNow;
            record.CreatedByUserId = CurrentUserId();
            await _db.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(sendTo))
            {
                var sent = await _emailSender.SendAsync(sendTo, "Your Pharmino Academy mailbox", DetailsHtml(address, req.Password!, record.DisplayName), DetailsText(address, req.Password!), ct);
                if (!sent) warnings.Add("The login details email couldn't be sent.");
            }

            _log.LogInformation("Admin {UserId} created mailbox {Address} ({Label}).", CurrentUserId(), address, req.Label);
            return Json(new { success = true, address, warnings });
        }

        // ---------------------------------------------------------------- manage one mailbox

        public class MailboxActionRequest
        {
            public string Address { get; set; } = "";
            /// <summary>label | name | password | quota | suspend | unsuspend | alias-add | alias-remove | forward-add | forward-remove | delete</summary>
            public string Action { get; set; } = "";
            public string? Value { get; set; }
            public int? QuotaMb { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage([FromBody] MailboxActionRequest req, CancellationToken ct)
        {
            var address = (req.Address ?? "").Trim().ToLowerInvariant();
            var at = address.LastIndexOf('@');
            if (at <= 0) return Fail("Unknown mailbox.");
            var account = address[..at];
            var domain = address[(at + 1)..];
            var domainCheck = await KnownDomainAsync(domain, ct);
            if (domainCheck is not null) return Fail(domainCheck);
            var value = req.Value?.Trim() ?? "";

            MailResult result;
            switch (req.Action)
            {
                case "label":
                    if (!MailboxLabels.IsValid(value)) return Fail("Choose Admin or Student.");
                    await UpsertRecordAsync(address, r => r.Label = value, ct);
                    result = MailResult.Ok;
                    break;
                case "name":
                    await UpsertRecordAsync(address, r => r.DisplayName = value.Length == 0 ? null : value[..Math.Min(120, value.Length)], ct);
                    result = MailResult.Ok;
                    break;
                case "password":
                    var problem = MailboxPasswordPolicy.Problem(address, req.Value ?? "");
                    if (problem is not null) return Fail(problem, "password");
                    result = await _mail.ChangePasswordAsync(domain, account, req.Value!, ct);
                    break;
                case "quota":
                    if (req.QuotaMb is < 1 or > 1_000_000) return Fail("Quota must be between 1 and 1,000,000 MB, or unlimited.");
                    result = await _mail.ChangeQuotaAsync(domain, account, req.QuotaMb, ct);
                    break;
                case "suspend":
                case "unsuspend":
                    result = await _mail.SetSuspendedAsync(domain, account, req.Action == "suspend", ct);
                    break;
                case "alias-add":
                    var alias = (value.Contains('@') ? value[..value.IndexOf('@')] : value).ToLowerInvariant();
                    if (!AccountRx.IsMatch(alias)) return Fail("That alias isn't a valid name.");
                    result = await _mail.AddAliasAsync(domain, account, alias, ct);
                    break;
                case "alias-remove":
                    result = await _mail.DeleteAliasAsync(domain, account, value, ct);
                    break;
                case "forward-add":
                    if (!AddressRx.IsMatch(value)) return Fail("That forwarding address isn't valid.");
                    result = await _mail.AddForwardAsync(domain, account, value, ct);
                    break;
                case "forward-remove":
                    result = await _mail.DeleteForwardAsync(domain, account, value, ct);
                    break;
                case "delete":
                    if (!string.Equals(value, address, StringComparison.OrdinalIgnoreCase)) return Fail("Type the full address to confirm.");
                    result = await _mail.DeleteAsync(domain, account, ct);
                    if (result.Success)
                    {
                        var rec = await _db.Mailboxes.FirstOrDefaultAsync(m => m.Address == address, ct);
                        if (rec is not null) { _db.Mailboxes.Remove(rec); await _db.SaveChangesAsync(ct); }
                    }
                    break;
                default:
                    return Fail("Unknown action.");
            }

            if (!result.Success) return Fail(result.Error!);
            _log.LogInformation("Admin {UserId} ran {Action} on mailbox {Address}.", CurrentUserId(), req.Action, address);
            return Json(new { success = true });
        }

        // ---------------------------------------------------------------- helpers

        private async Task<string?> KnownDomainAsync(string domain, CancellationToken ct)
        {
            if (!_mail.IsConfigured) return "The mail server connection isn't set up yet.";
            var (domains, error) = await _mail.ListDomainsAsync(ct);
            if (error is not null) return error;
            return domains.Contains(domain) ? null : "That domain isn't a mail domain on this server.";
        }

        private async Task UpsertRecordAsync(string address, Action<Mailbox> change, CancellationToken ct)
        {
            var rec = await _db.Mailboxes.FirstOrDefaultAsync(m => m.Address == address, ct);
            if (rec is null)
            {
                // A mailbox made outside this panel (e.g. in Hestia itself): start tracking it now.
                rec = new Mailbox { Address = address, Label = MailboxLabels.Student, CreatedAt = DateTime.UtcNow, CreatedByUserId = CurrentUserId() };
                _db.Mailboxes.Add(rec);
            }
            change(rec);
            await _db.SaveChangesAsync(ct);
        }

        private int? CurrentUserId()
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private JsonResult Fail(string message, string? field = null)
            => Json(new { success = false, message, field });

        private string DetailsText(string address, string password)
            => $"Your mailbox is ready.\n\nAddress: {address}\nPassword: {password}\n\nIncoming (IMAP): {_options.MailHost}, port 993, SSL\nOutgoing (SMTP): {_options.MailHost}, port 465, SSL\n" +
               (string.IsNullOrEmpty(_options.WebmailUrl) ? "" : $"Webmail: {_options.WebmailUrl}\n") +
               "\nPlease change the password after your first sign-in.";

        private string DetailsHtml(string address, string password, string? name)
        {
            static string E(string s) => System.Net.WebUtility.HtmlEncode(s);
            var webmail = string.IsNullOrEmpty(_options.WebmailUrl) ? "" : $"<p>Webmail: <a href=\"{E(_options.WebmailUrl)}\">{E(_options.WebmailUrl)}</a></p>";
            return $"<p>{(name is null ? "Hello" : "Hello " + E(name))},</p><p>Your Pharmino Academy mailbox is ready.</p>" +
                   $"<p><b>Address:</b> {E(address)}<br><b>Password:</b> <code>{E(password)}</code></p>" +
                   $"<p>Incoming (IMAP): {E(_options.MailHost)}, port 993, SSL<br>Outgoing (SMTP): {E(_options.MailHost)}, port 465, SSL</p>{webmail}" +
                   "<p>Please change the password after your first sign-in.</p>";
        }
    }

    public record EmailsPageModel(bool Configured, bool IsDemo, IReadOnlyList<string> Domains, string? Error, string MailHost, string WebmailUrl);
}
