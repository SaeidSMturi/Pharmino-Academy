using System.Collections.Generic;

namespace Application.DTOs
{
    public class SeoOnPageResult
    {
        public string Url { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string? ResponseMessage { get; set; }

        // Core Tags
        public string? Title { get; set; }
        public int TitleLength => Title?.Length ?? 0;
        public string TitleStatus { get; set; } = "Unknown"; // "Excellent", "Warning", "Critical"
        public string TitleMessage { get; set; } = string.Empty;

        public string? MetaDescription { get; set; }
        public int MetaDescriptionLength => MetaDescription?.Length ?? 0;
        public string MetaDescriptionStatus { get; set; } = "Unknown";
        public string MetaDescriptionMessage { get; set; } = string.Empty;

        public string? CanonicalUrl { get; set; }
        public bool HasCanonical { get; set; }
        public bool CanonicalMatchesUrl { get; set; }

        public string? RobotsMeta { get; set; }
        public bool IsNoindex { get; set; }
        public bool IsNofollow { get; set; }

        // Headings
        public List<HeadingItem> Headings { get; set; } = new();
        public int H1Count { get; set; }
        public string HeadingStatus { get; set; } = "Unknown";
        public string HeadingMessage { get; set; } = string.Empty;

        // Structured Data & Social Meta
        public List<string> SchemaTypes { get; set; } = new();
        public Dictionary<string, string> OpenGraphTags { get; set; } = new();
        public Dictionary<string, string> TwitterCardTags { get; set; } = new();

        // Content
        public int WordCount { get; set; }
        public string ReadabilityEstimation { get; set; } = string.Empty;
        public double KeywordDensityPercentage { get; set; }
        public int KeywordCount { get; set; }
        public string KeywordStatus { get; set; } = "Unknown";
        public string KeywordMessage { get; set; } = string.Empty;

        // Media
        public int TotalImages { get; set; }
        public int ImagesMissingAltCount { get; set; }
        public List<string> ImagesMissingAltList { get; set; } = new();

        // Links
        public int TotalLinks { get; set; }
        public int InternalLinksCount { get; set; }
        public int ExternalLinksCount { get; set; }

        // Page specific score
        public int PageScore { get; set; }
    }

    public class HeadingItem
    {
        public string Type { get; set; } = string.Empty; // H1, H2, H3, H4, H5, H6
        public string Content { get; set; } = string.Empty;
    }
}
