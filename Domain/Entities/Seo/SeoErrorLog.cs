using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Seo
{
    public class SeoErrorLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Referrer { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public int StatusCode { get; set; } // 404 or 5xx

        public int OccurrenceCount { get; set; } = 1;

        public DateTime LastOccurred { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;
    }
}
