using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.RecentActivity
{
    public class RecentActivityPriority
    {
        [Key]
        public int PriorityId { get; set; }
        public string Title { get; set; } = string.Empty;
        public virtual IEnumerable<RecentActivity> RecentActivities { get; set; } = new List<RecentActivity>();
    }
}
