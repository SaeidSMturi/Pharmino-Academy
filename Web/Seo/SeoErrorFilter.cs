using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Web.Seo
{
    /// <summary>
    /// Decides which 404/5xx responses are real SEO problems. Almost all 404s on a public server come from
    /// hacking scanners probing for files (/.env, /wp-admin, /config/aws.yml, credentials.json …); those are
    /// not broken pages, so they are neither logged nor counted. A 404/5xx is kept only when the address
    /// looks like one of this site's own public pages: no file extension, and the first path segment is a
    /// real public controller or route of the app (e.g. /Course/999 for a deleted course).
    /// </summary>
    public sealed class SeoErrorFilter
    {
        private readonly IActionDescriptorCollectionProvider _actions;
        private HashSet<string>? _publicSegments;
        private int _builtForVersion = -1;

        public SeoErrorFilter(IActionDescriptorCollectionProvider actions)
        {
            _actions = actions;
        }

        public bool IsWorthLogging(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            // A real request path never carries '?' or '#'; such rows (and encoded dots like %2e) are scanner junk.
            var clean = path.Trim();
            if (clean.IndexOfAny(new[] { '?', '#' }) >= 0 || clean.Contains("%2e", StringComparison.OrdinalIgnoreCase)) return false;
            if (!clean.StartsWith('/')) clean = "/" + clean;
            if (clean == "/") return true;

            // The error page itself (the real failing page is logged instead) and broken script links like /home/null.
            if (clean.Equals("/home/error", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = clean.Trim('/').Split('/');
            if (parts.Any(p => p.Equals("null", StringComparison.OrdinalIgnoreCase) || p.Equals("undefined", StringComparison.OrdinalIgnoreCase))) return false;

            // Pages on this site never have a file extension; probes nearly always do (.php, .env, .json, .yml …).
            if (Path.HasExtension(clean) || clean.Contains("/.")) return false;

            // Logged-in areas, APIs, sign-in and payment pages are not indexed, so they are not SEO errors.
            if (SeoSite.IsNoIndexPath(clean)) return false;

            var first = clean.TrimStart('/').Split('/')[0];
            return PublicSegments().Contains(first);
        }

        private HashSet<string> PublicSegments()
        {
            var collection = _actions.ActionDescriptors;
            if (_publicSegments != null && _builtForVersion == collection.Version) return _publicSegments;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in collection.Items.OfType<ControllerActionDescriptor>())
            {
                if (action.RouteValues.TryGetValue("area", out var area) && !string.IsNullOrEmpty(area)) continue;

                var template = action.AttributeRouteInfo?.Template;
                if (template == null)
                {
                    set.Add(action.ControllerName); // conventional route: /{controller}/{action}/{id?}
                    continue;
                }

                var segment = template.TrimStart('~', '/').Split('/')[0]
                    .Replace("[controller]", action.ControllerName, StringComparison.OrdinalIgnoreCase)
                    .Replace("[action]", action.ActionName, StringComparison.OrdinalIgnoreCase);
                if (segment.Length > 0 && !segment.Contains('{')) set.Add(segment);
            }

            _publicSegments = set;
            _builtForVersion = collection.Version;
            return set;
        }
    }
}
