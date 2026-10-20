using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Seo
{
    public class PageSeo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string PagePath { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Title { get; set; }

        [MaxLength(1000)]
        public string? MetaDescription { get; set; }

        [MaxLength(500)]
        public string? MetaKeywords { get; set; }

        [MaxLength(100)]
        public string? MetaRobots { get; set; }

        [MaxLength(500)]
        public string? CanonicalUrl { get; set; }

        [MaxLength(300)]
        public string? OgTitle { get; set; }

        [MaxLength(1000)]
        public string? OgDescription { get; set; }

        [MaxLength(500)]
        public string? OgImage { get; set; }

        [MaxLength(50)]
        public string? OgType { get; set; }

        public string? JsonLd { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
