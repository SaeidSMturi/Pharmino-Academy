using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Seo
{
    public class SeoRedirect
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string OldUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string NewUrl { get; set; } = string.Empty;

        public bool IsPermanent { get; set; } = true; // true = 301, false = 302

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }
}
