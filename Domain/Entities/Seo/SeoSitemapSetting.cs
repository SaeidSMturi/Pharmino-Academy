using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Seo
{
    public class SeoSitemapSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Key { get; set; } = string.Empty; // e.g., "Section.Posts.ChangeFreq", "CustomUrl", "ExcludedUrl"

        [MaxLength(500)]
        public string? Value { get; set; } // e.g., "weekly", "0.8", or the URL path / pattern to exclude/include

        public double? CustomPriority { get; set; }

        [MaxLength(50)]
        public string? CustomChangeFreq { get; set; }

        public bool IsCustomLink { get; set; } = false;

        public bool IsExcludedPath { get; set; } = false;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
