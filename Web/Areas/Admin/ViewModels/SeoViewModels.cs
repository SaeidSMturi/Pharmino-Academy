using System.Collections.Generic;
using Domain.Entities.Seo;

namespace Web.Areas.Admin.ViewModels
{
    public class SeoDashboardViewModel
    {
        public int HealthScore { get; set; }
        public bool IsGlobalNoindexActive { get; set; }
        public bool HasRobotsTxt { get; set; }
        public bool HasSitemapIndex { get; set; }

        public List<SeoPageScan> PageScans { get; set; } = new();
        public List<SeoImage> MissingAltImages { get; set; } = new();
        public List<SeoErrorLog> CriticalErrors { get; set; } = new();
        public List<SeoRedirect> Redirects { get; set; } = new();

        // Statistical Aggregates
        public int TotalPagesScanned => PageScans.Count;
        public int SuccessfulPages => PageScans.Count(p => p.ResponseStatus != null && p.ResponseStatus.Contains("Success"));
        public int FailedPages => PageScans.Count - SuccessfulPages;
        public int PagesMissingMetaDescription => PageScans.Count(p => string.IsNullOrWhiteSpace(p.MetaDescription));
        public int PagesInvalidMetaLength => PageScans.Count(p => !string.IsNullOrWhiteSpace(p.MetaDescription) && (p.MetaDescription.Length < 120 || p.MetaDescription.Length > 160));
        public int PagesMissingH1 => PageScans.Count(p => p.H1Count == 0);
        public int PagesMultipleH1 => PageScans.Count(p => p.H1Count > 1);
        public int TotalImagesMissingAlt => MissingAltImages.Count;
        public int TotalUnresolvedErrors => CriticalErrors.Count(e => !e.IsResolved);
    }

    public class AddRedirectViewModel
    {
        public string OldUrl { get; set; } = string.Empty;
        public string NewUrl { get; set; } = string.Empty;
        public bool IsPermanent { get; set; } = true;
    }
}
