using System.Collections.Generic;

namespace Web.Areas.User.Models
{
    /// <summary>One enrolled course drawn as a glass folder on the student's My Courses page.</summary>
    public class CourseFolderViewModel
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;

        /// <summary>Back-panel picture; null draws the brand gradient instead.</summary>
        public string? ImageUrl { get; set; }

        public bool IsBundle { get; set; }

        /// <summary>Second line on the glass, e.g. "12 lessons · 40% done".</summary>
        public string Meta { get; set; } = string.Empty;

        /// <summary>0–100; drawn as a thin bar on the glass. Null hides it (bundles).</summary>
        public int? ProgressPercent { get; set; }

        public string CourseUrl { get; set; } = string.Empty;

        /// <summary>The lesson to resume, or null when the course has no lessons.</summary>
        public string? ContinueUrl { get; set; }

        public bool HasTests { get; set; }

        /// <summary>Back to front: the last one sits nearest the glass. At most four.</summary>
        public List<CourseFolderFile> Files { get; set; } = new();
    }

    public class CourseFolderFile
    {
        public string Name { get; set; } = string.Empty;
        public string? Meta { get; set; }
        public string Href { get; set; } = string.Empty;

        /// <summary>"lesson", "pdf", "course" (inside a bundle) or "all".</summary>
        public string Kind { get; set; } = "lesson";

        /// <summary>Font Awesome icon class drawn on the sheet.</summary>
        public string Icon { get; set; } = "fa-play";

        /// <summary>0–100 for a lesson in progress; null otherwise.</summary>
        public int? Progress { get; set; }
        public bool Done { get; set; }
    }
}
