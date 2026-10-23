using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Course
{
    public class CourseBundleItem
    {
        [Key]
        public int Id { get; set; }

        public int BundleId { get; set; }

        [ForeignKey("BundleId")]
        public virtual Course Bundle { get; set; } = null!;

        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; } = null!;

        public int OrderIndex { get; set; } = 0;

        public DateTime AddedDate { get; set; } = DateTime.UtcNow;
    }
}
