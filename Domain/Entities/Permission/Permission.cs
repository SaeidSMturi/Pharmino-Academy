using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Permission
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }

        [Required]
        [MaxLength(100)]
        public string PermissionName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        #region Relations

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

        #endregion
    }
}
