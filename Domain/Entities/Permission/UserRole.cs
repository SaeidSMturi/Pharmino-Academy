using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Permission
{
    public class UserRole
    {
        [Key]
        public int UserRoleId { get; set; }

        public int UserId { get; set; }

        public int RoleId { get; set; }

        public DateTime AssignedDate { get; set; }

        public int? AssignedBy { get; set; }

        #region Relations

        public virtual User.User User { get; set; } = null!;
        public virtual Role Role { get; set; } = null!;

        #endregion
    }
}
