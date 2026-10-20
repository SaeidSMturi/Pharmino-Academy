using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Seo
{
    public class SeoImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AltText { get; set; }

        [Required]
        [MaxLength(500)]
        public string FoundOnPage { get; set; } = string.Empty;

        public DateTime FoundAt { get; set; } = DateTime.UtcNow;
    }
}
