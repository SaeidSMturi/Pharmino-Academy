using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Web.Services.Mail
{
    /// <summary>Connection to the HestiaCP panel that hosts the mail domain ("Hestia" section of appsettings).</summary>
    public class HestiaOptions
    {
        /// <summary>The control panel, e.g. https://server.pharminoacademy.com:8083</summary>
        public string Url { get; set; } = "";
        /// <summary>The Hestia user that owns the mail domain (e.g. PAcademy).</summary>
        public string User { get; set; } = "";
        /// <summary>API access key pair (Hestia → User → Access keys). Needs the mail commands listed in <see cref="HestiaMailServer"/>.</summary>
        public string AccessKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        /// <summary>Optional: connect to this address instead of resolving the panel's host name (the certificate is still checked by name).</summary>
        public string ConnectAddress { get; set; } = "";
        /// <summary>Shown on the create page as "connection settings" for the new mailbox.</summary>
        public string MailHost { get; set; } = "mail.pharminoacademy.com";
        public string WebmailUrl { get; set; } = "";
        /// <summary>
        /// Mail domain(s) to use when the access key isn't allowed to list domains (v-list-mail-domains is not part of
        /// every key permission set, e.g. "mail-accounts" may not include it).
        /// </summary>
        public string[] Domains { get; set; } = new[] { "pharminoacademy.com" };

        public bool Configured => Url.Length > 0 && User.Length > 0 && AccessKey.Length > 0 && SecretKey.Length > 0;
    }

    public record MailboxInfo(
        string Address,
        string Account,
        string Domain,
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string> Forwards,
        bool ForwardOnly,
        int? QuotaMb,
        int UsedMb,
        bool Suspended,
        DateTime? CreatedAt);

    /// <summary>Null error means it worked.</summary>
    public record MailResult(string? Error)
    {
        public static readonly MailResult Ok = new((string?)null);
        public bool Success => Error is null;
    }

    public interface IMailServerAdmin
    {
        /// <summary>False when the Hestia connection isn't set up (the pages then say so instead of failing).</summary>
        bool IsConfigured { get; }
        /// <summary>True for the in-memory stand-in used during local development.</summary>
        bool IsDemo { get; }
        Task<(IReadOnlyList<string> Domains, string? Error)> ListDomainsAsync(CancellationToken ct = default);
        Task<(IReadOnlyList<MailboxInfo> Accounts, string? Error)> ListAccountsAsync(string domain, CancellationToken ct = default);
        Task<MailResult> CreateAsync(string domain, string account, string password, int? quotaMb, CancellationToken ct = default);
        Task<MailResult> ChangePasswordAsync(string domain, string account, string password, CancellationToken ct = default);
        Task<MailResult> ChangeQuotaAsync(string domain, string account, int? quotaMb, CancellationToken ct = default);
        Task<MailResult> SetSuspendedAsync(string domain, string account, bool suspended, CancellationToken ct = default);
        Task<MailResult> AddAliasAsync(string domain, string account, string alias, CancellationToken ct = default);
        Task<MailResult> DeleteAliasAsync(string domain, string account, string alias, CancellationToken ct = default);
        Task<MailResult> AddForwardAsync(string domain, string account, string forwardTo, CancellationToken ct = default);
        Task<MailResult> DeleteForwardAsync(string domain, string account, string forwardTo, CancellationToken ct = default);
        Task<MailResult> DeleteAsync(string domain, string account, CancellationToken ct = default);
    }

    /// <summary>
    /// Talks to the HestiaCP REST API (https://hestiacp.com/docs/server-administration/rest-api).
    /// Commands used: v-list-mail-domains, v-list-mail-accounts, v-add-mail-account, v-change-mail-account-password,
    /// v-change-mail-account-quota, v-suspend-mail-account, v-unsuspend-mail-account, v-add-mail-account-alias,
    /// v-delete-mail-account-alias, v-add-mail-account-forward, v-delete-mail-account-forward, v-delete-mail-account.
    /// </summary>
    public class HestiaMailServer(HestiaOptions options, IHttpClientFactory http, ILogger<HestiaMailServer> log) : IMailServerAdmin
    {
        public const string HttpClientName = "hestia";
        public bool IsConfigured => options.Configured;
        public bool IsDemo => false;

        public async Task<(IReadOnlyList<string>, string?)> ListDomainsAsync(CancellationToken ct = default)
        {
            var (json, error) = await QueryAsync("v-list-mail-domains", ct, options.User, "json");
            if (json is null)
            {
                // The key may only be allowed the mail-account commands: fall back to the configured domain(s).
                var fallback = options.Domains.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim().ToLowerInvariant()).ToList();
                if (options.Configured && fallback.Count > 0 && !error!.StartsWith("Couldn't reach", StringComparison.Ordinal))
                {
                    log.LogInformation("Listing mail domains was refused ({Error}); using the configured domain list.", error);
                    return (fallback, null);
                }
                return (Array.Empty<string>(), error);
            }
            var list = json.Value.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).OrderBy(d => d).ToList();
            return (list, null);
        }

        public async Task<(IReadOnlyList<MailboxInfo>, string?)> ListAccountsAsync(string domain, CancellationToken ct = default)
        {
            var (json, error) = await QueryAsync("v-list-mail-accounts", ct, options.User, domain, "json");
            if (json is null) return (Array.Empty<MailboxInfo>(), error);
            var list = new List<MailboxInfo>();
            foreach (var p in json.Value.EnumerateObject())
            {
                var v = p.Value;
                string S(string name) => v.TryGetProperty(name, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "";
                static List<string> Split(string s) => s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                var account = p.Name.ToLowerInvariant();
                var aliases = Split(S("ALIAS")).Select(a => a.Contains('@') ? a : a + "@" + domain).ToList();
                var quota = S("QUOTA");
                int? quotaMb = int.TryParse(quota, out var q) ? q : null; // "unlimited" -> null
                int.TryParse(S("U_DISK"), out var used);
                DateTime? created = DateTime.TryParseExact(S("DATE") + " " + S("TIME"), "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var c) ? c : null;

                list.Add(new MailboxInfo(account + "@" + domain, account, domain, aliases, Split(S("FWD")),
                    S("FWD_ONLY") == "yes", quotaMb, used, S("SUSPENDED") == "yes", created));
            }
            return (list, null);
        }

        public Task<MailResult> CreateAsync(string domain, string account, string password, int? quotaMb, CancellationToken ct = default)
            => RunAsync("v-add-mail-account", ct, options.User, domain, account, password, quotaMb?.ToString(CultureInfo.InvariantCulture) ?? "unlimited");

        public Task<MailResult> ChangePasswordAsync(string domain, string account, string password, CancellationToken ct = default)
            => RunAsync("v-change-mail-account-password", ct, options.User, domain, account, password);

        public Task<MailResult> ChangeQuotaAsync(string domain, string account, int? quotaMb, CancellationToken ct = default)
            => RunAsync("v-change-mail-account-quota", ct, options.User, domain, account, quotaMb?.ToString(CultureInfo.InvariantCulture) ?? "unlimited");

        public Task<MailResult> SetSuspendedAsync(string domain, string account, bool suspended, CancellationToken ct = default)
            => RunAsync(suspended ? "v-suspend-mail-account" : "v-unsuspend-mail-account", ct, options.User, domain, account);

        public Task<MailResult> AddAliasAsync(string domain, string account, string alias, CancellationToken ct = default)
            => RunAsync("v-add-mail-account-alias", ct, options.User, domain, account, LocalPart(alias));

        public Task<MailResult> DeleteAliasAsync(string domain, string account, string alias, CancellationToken ct = default)
            => RunAsync("v-delete-mail-account-alias", ct, options.User, domain, account, LocalPart(alias));

        public Task<MailResult> AddForwardAsync(string domain, string account, string forwardTo, CancellationToken ct = default)
            => RunAsync("v-add-mail-account-forward", ct, options.User, domain, account, forwardTo);

        public Task<MailResult> DeleteForwardAsync(string domain, string account, string forwardTo, CancellationToken ct = default)
            => RunAsync("v-delete-mail-account-forward", ct, options.User, domain, account, forwardTo);

        public Task<MailResult> DeleteAsync(string domain, string account, CancellationToken ct = default)
            => RunAsync("v-delete-mail-account", ct, options.User, domain, account);

        private static string LocalPart(string alias) => alias.Contains('@') ? alias[..alias.IndexOf('@')] : alias;

        private FormUrlEncodedContent Form(string cmd, bool returnCode, string[] args)
        {
            var fields = new Dictionary<string, string>
            {
                ["hash"] = $"{options.AccessKey}:{options.SecretKey}",
                ["cmd"] = cmd,
            };
            if (returnCode) fields["returncode"] = "yes";
            for (var i = 0; i < args.Length; i++) fields["arg" + (i + 1)] = args[i];
            return new FormUrlEncodedContent(fields);
        }

        /// <summary>Action commands: Hestia answers with its exit code ("0" = done).</summary>
        private async Task<MailResult> RunAsync(string cmd, CancellationToken ct, params string[] args)
        {
            if (!options.Configured) return new MailResult(NotConfigured);
            try
            {
                using var res = await http.CreateClient(HttpClientName).PostAsync(options.Url.TrimEnd('/') + "/api/", Form(cmd, true, args), ct);
                var body = (await res.Content.ReadAsStringAsync(ct)).Trim();
                if (res.IsSuccessStatusCode && body == "0") return MailResult.Ok;
                log.LogWarning("Hestia {Cmd} failed: HTTP {Status}, answer {Body}", cmd, (int)res.StatusCode, body.Length > 60 ? body[..60] : body);
                return new MailResult(Explain(body));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                log.LogError(ex, "Could not reach the Hestia API for {Cmd}.", cmd);
                return new MailResult("Couldn't reach the mail server's control panel. Try again in a minute.");
            }
        }

        /// <summary>List commands: Hestia answers with JSON (or an error text).</summary>
        private async Task<(JsonElement?, string?)> QueryAsync(string cmd, CancellationToken ct, params string[] args)
        {
            if (!options.Configured) return (null, NotConfigured);
            try
            {
                using var res = await http.CreateClient(HttpClientName).PostAsync(options.Url.TrimEnd('/') + "/api/", Form(cmd, false, args), ct);
                var body = (await res.Content.ReadAsStringAsync(ct)).Trim();
                if (res.IsSuccessStatusCode && body.StartsWith('{'))
                    return (JsonDocument.Parse(body).RootElement.Clone(), null);
                if (res.IsSuccessStatusCode && body.Length == 0)
                    return (JsonDocument.Parse("{}").RootElement.Clone(), null);
                log.LogWarning("Hestia {Cmd} failed: HTTP {Status}, answer {Body}", cmd, (int)res.StatusCode, body.Length > 60 ? body[..60] : body);
                return (null, Explain(body));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                log.LogError(ex, "Could not reach the Hestia API for {Cmd}.", cmd);
                return (null, "Couldn't reach the mail server's control panel. Try again in a minute.");
            }
        }

        private const string NotConfigured = "The mail server connection isn't set up yet (Hestia section in appsettings).";

        /// <summary>Hestia exit codes: https://github.com/hestiacp/hestiacp/blob/main/func/main.sh</summary>
        private static string Explain(string code) => code.Contains("IP is not allowed", StringComparison.OrdinalIgnoreCase)
            ? "HestiaCP refused this server's IP address. Add it under Server Settings → Security → API → Allowed IP addresses."
            : code switch
        {
            "1" or "2" => "The mail server rejected one of the values (check the address and password).",
            "3" => "That mailbox or domain doesn't exist on the mail server.",
            "4" => "That address is already taken.",
            "5" => "That mailbox is suspended.",
            "6" => "That mailbox is already active.",
            "8" => "The mail domain has reached its mailbox limit.",
            "9" or "10" => "The mail server refused the API key (it may not be allowed to run mail commands).",
            _ => "The mail server couldn't do that right now (code " + (code.Length > 12 ? "?" : code) + ").",
        };
    }

    /// <summary>
    /// Local development only, when no Hestia connection is set: keeps mailboxes in memory so the pages can be tried out.
    /// Never registered in production.
    /// </summary>
    public class DemoMailServer : IMailServerAdmin
    {
        private readonly ConcurrentDictionary<string, MailboxInfo> _boxes = new();
        private const string Domain = "pharminoacademy.com";

        public DemoMailServer()
        {
            var now = DateTime.Now;
            Seed("info", now.AddDays(-40), 2048, 310);
            Seed("leila.karimi", now.AddDays(-21).AddHours(-3), 1024, 88);
            Seed("sara.hosseini", now.AddDays(-9).AddHours(5), null, 12, aliases: new[] { "sara@" + Domain });
            Seed("reza.ahmadi", now.AddDays(-2).AddHours(-7), 512, 3, suspended: true);
            Seed("support", now.AddHours(-30), null, 0, forwards: new[] { "it-team@example.com" });
        }

        private void Seed(string acc, DateTime created, int? quota, int used, string[]? aliases = null, string[]? forwards = null, bool suspended = false)
            => _boxes[acc] = new MailboxInfo(acc + "@" + Domain, acc, Domain, aliases ?? Array.Empty<string>(), forwards ?? Array.Empty<string>(), false, quota, used, suspended, created);

        public bool IsConfigured => true;
        public bool IsDemo => true;

        public Task<(IReadOnlyList<string>, string?)> ListDomainsAsync(CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<string>, string?)>((new[] { Domain }, null));

        public Task<(IReadOnlyList<MailboxInfo>, string?)> ListAccountsAsync(string domain, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<MailboxInfo>, string?)>((_boxes.Values.Where(b => b.Domain == domain).ToList(), null));

        public Task<MailResult> CreateAsync(string domain, string account, string password, int? quotaMb, CancellationToken ct = default)
        {
            var box = new MailboxInfo(account + "@" + domain, account, domain, Array.Empty<string>(), Array.Empty<string>(), false, quotaMb, 0, false, DateTime.Now);
            return Task.FromResult(_boxes.TryAdd(account, box) ? MailResult.Ok : new MailResult("That address is already taken."));
        }

        private Task<MailResult> Update(string account, Func<MailboxInfo, MailboxInfo> change)
        {
            if (!_boxes.TryGetValue(account, out var box)) return Task.FromResult(new MailResult("That mailbox doesn't exist on the mail server."));
            _boxes[account] = change(box);
            return Task.FromResult(MailResult.Ok);
        }

        public Task<MailResult> ChangePasswordAsync(string domain, string account, string password, CancellationToken ct = default) => Update(account, b => b);
        public Task<MailResult> ChangeQuotaAsync(string domain, string account, int? quotaMb, CancellationToken ct = default) => Update(account, b => b with { QuotaMb = quotaMb });
        public Task<MailResult> SetSuspendedAsync(string domain, string account, bool suspended, CancellationToken ct = default) => Update(account, b => b with { Suspended = suspended });
        public Task<MailResult> AddAliasAsync(string domain, string account, string alias, CancellationToken ct = default) => Update(account, b => b with { Aliases = b.Aliases.Append(alias.Contains('@') ? alias : alias + "@" + domain).Distinct().ToList() });
        public Task<MailResult> DeleteAliasAsync(string domain, string account, string alias, CancellationToken ct = default) => Update(account, b => b with { Aliases = b.Aliases.Where(a => a != alias).ToList() });
        public Task<MailResult> AddForwardAsync(string domain, string account, string forwardTo, CancellationToken ct = default) => Update(account, b => b with { Forwards = b.Forwards.Append(forwardTo).Distinct().ToList() });
        public Task<MailResult> DeleteForwardAsync(string domain, string account, string forwardTo, CancellationToken ct = default) => Update(account, b => b with { Forwards = b.Forwards.Where(f => f != forwardTo).ToList() });
        public Task<MailResult> DeleteAsync(string domain, string account, CancellationToken ct = default)
            => Task.FromResult(_boxes.TryRemove(account, out _) ? MailResult.Ok : new MailResult("That mailbox doesn't exist on the mail server."));
    }

    public static class MailboxPasswordPolicy
    {
        /// <summary>Same rule as the webmail's own password change, so a password set here also passes there.</summary>
        public static string? Problem(string address, string password)
        {
            if (password.Length < 10) return "Use at least 10 characters.";
            if (password.Length > 128) return "Use at most 128 characters.";
            if (password.Any(char.IsControl) || password.Any(char.IsWhiteSpace)) return "The password can't contain spaces or control characters.";
            if (!password.Any(char.IsLetter) || !password.Any(c => !char.IsLetter(c))) return "Mix letters with numbers or symbols.";
            var local = address.Split('@')[0];
            if (local.Length >= 4 && password.Contains(local, StringComparison.OrdinalIgnoreCase)) return "Don't put the address in the password.";
            if (password.Distinct().Count() < 5) return "Use a less repetitive password.";
            return null;
        }
    }
}
