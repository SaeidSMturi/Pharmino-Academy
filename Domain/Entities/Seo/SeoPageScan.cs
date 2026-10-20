using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Seo
{
    public class SeoPageScan
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Title { get; set; }

        public string? MetaDescription { get; set; }

        public int H1Count { get; set; }

        public int MissingAltCount { get; set; }

        public bool HasNoIndex { get; set; }

        [MaxLength(100)]
        public string? ResponseStatus { get; set; }

        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    }
}
