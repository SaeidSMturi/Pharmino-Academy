namespace Application.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> UserHasPermissionAsync(int userId, string permissionName);
        Task<List<string>> GetUserPermissionsAsync(int userId);
        Task<List<string>> GetUserRolesAsync(int userId);
        Task<bool> AssignRoleToUserAsync(int userId, int roleId, int? assignedBy = null);
        Task<bool> RemoveRoleFromUserAsync(int userId, int roleId);
        Task<bool> UserHasRoleAsync(int userId, string roleName);
        Task<bool> UserHasAnyRoleAsync(int userId);
    }
}
