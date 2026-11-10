using Ganss.Xss;

namespace Web.Services.Content
{
    /// <summary>
    /// Server-side clean-up of article HTML before it is saved. The editor cleans its output in the browser too,
    /// but a request can be sent without the editor, so this is the one that counts.
    /// Allows exactly what the article editor produces: headings, text formatting, lists, quotes, code, tables,
    /// links and images inside figures. No scripts, styles, event handlers, iframes, forms or data: URLs.
    /// </summary>
    public sealed class ArticleHtmlSanitizer
    {
        private readonly HtmlSanitizer _sanitizer;

        public ArticleHtmlSanitizer()
        {
            _sanitizer = new HtmlSanitizer();

            _sanitizer.AllowedTags.Clear();
            foreach (var tag in new[]
            {
                "h2", "h3", "h4", "p", "span", "strong", "b", "em", "i", "u", "s", "strike", "code", "pre",
                "blockquote", "ul", "ol", "li", "table", "thead", "tbody", "tr", "th", "td",
                "figure", "figcaption", "img", "a", "hr", "br"
            })
                _sanitizer.AllowedTags.Add(tag);

            _sanitizer.AllowedAttributes.Clear();
            foreach (var attr in new[]
            {
                "src", "alt", "title", "loading", "width", "height", "data-align",
                "href", "target", "rel", "colspan", "rowspan", "class"
            })
                _sanitizer.AllowedAttributes.Add(attr);

            // No inline styles at all (the editor strips them too).
            _sanitizer.AllowedCssProperties.Clear();
            _sanitizer.AllowedAtRules.Clear();

            _sanitizer.AllowedSchemes.Clear();
            _sanitizer.AllowedSchemes.Add("http");
            _sanitizer.AllowedSchemes.Add("https");
            _sanitizer.AllowedSchemes.Add("mailto");

            // Links that open a new tab must not give that page control over ours.
            _sanitizer.PostProcessNode += (_, e) =>
            {
                if (e.Node is AngleSharp.Dom.IElement el && el.NodeName == "A" && el.GetAttribute("target") == "_blank")
                    el.SetAttribute("rel", "noopener noreferrer");
            };
        }

        public string Sanitize(string? html) =>
            string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html).Trim();
    }
}
