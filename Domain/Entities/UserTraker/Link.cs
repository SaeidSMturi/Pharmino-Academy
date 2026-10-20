using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.UserTraker
{
    public class Link
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(2048)]
        public string VisitedLink { get; set; } = string.Empty;

        // Renamed for clarity
        public DateTime VisitedAt { get; set; }

        public int VisitorId { get; set; }

        [ForeignKey("VisitorId")]
        public virtual Visitor Visitor { get; set; } = null!;
    }
}
