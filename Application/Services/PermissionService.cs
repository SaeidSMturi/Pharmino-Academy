using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Domain.Entities.Permission;

namespace Application.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IApplicationDbContext _context;

        public PermissionService(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> UserHasPermissionAsync(int userId, string permissionName)
        {
            // Check if user has the permission through their roles
            var hasPermission = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Include(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .AnyAsync(ur => ur.Role.IsActive &&
                               ur.Role.RolePermissions.Any(rp => rp.Permission.PermissionName == permissionName));

            return hasPermission;
        }

        public async Task<List<string>> GetUserPermissionsAsync(int userId)
        {
            var permissions = await _context.UserRoles
                .Where(ur => ur.UserId == userId && ur.Role.IsActive)
                .Include(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.PermissionName))
                .Distinct()
                .ToListAsync();

            return permissions;
        }

        public async Task<List<string>> GetUserRolesAsync(int userId)
        {
            var roles = await _context.UserRoles
                .Where(ur => ur.UserId == userId && ur.Role.IsActive)
                .Include(ur => ur.Role)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();

            return roles;
        }

        public async Task<bool> AssignRoleToUserAsync(int userId, int roleId, int? assignedBy = null)
        {
            // Check if user already has this role
            var existingRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

            if (existingRole != null)
                return false; // Already has this role

            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedDate = DateTime.Now,
                AssignedBy = assignedBy
            };

            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RemoveRoleFromUserAsync(int userId, int roleId)
        {
            var userRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

            if (userRole == null)
                return false;

            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UserHasRoleAsync(int userId, string roleName)
        {
            return await _context.UserRoles
                .Where(ur => ur.UserId == userId && ur.Role.IsActive)
                .Include(ur => ur.Role)
                .AnyAsync(ur => ur.Role.RoleName == roleName);
        }

        public async Task<bool> UserHasAnyRoleAsync(int userId)
        {
            return await _context.UserRoles
                .AnyAsync(ur => ur.UserId == userId && ur.Role.IsActive);
        }
    }
}
