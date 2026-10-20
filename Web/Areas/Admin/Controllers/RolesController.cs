using Web.Extensions; // Added for GetUserId
using Application.Interfaces; // Added
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Added for Logging
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    public class RolesController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly IPermissionService _permissionService; // Added
        private readonly ILogger<RolesController> _logger; // Added for Professional Logging

        public RolesController(WebContext context, IPermissionService permissionService, ILogger<RolesController> logger)
        {
            _context = context;
            _permissionService = permissionService; // Added
            _logger = logger;
        }

        [Permission("Roles.View")]
        public async Task<IActionResult> Index()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("Admin user {UserId} is fetching the roles list.", currentUserId);

            var roles = await _context.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .Include(r => r.UserRoles)
                .OrderByDescending(r => r.CreateDate)
                .ToListAsync();

            _logger.LogInformation("Successfully retrieved {Count} roles.", roles.Count);
            return View(roles);
        }

        [Permission("Roles.View")]
        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("Admin user {UserId} requested details for Role ID: {RoleId}.", currentUserId, id);

            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .Include(r => r.UserRoles)
                    .ThenInclude(ur => ur.User)
                .FirstOrDefaultAsync(r => r.RoleId == id);

            if (role == null)
            {
                _logger.LogWarning("Role with ID {RoleId} was not found during details request by User {UserId}.", id, currentUserId);
                TempData["ErrorMessage"] = "Role not found";
                return RedirectToAction(nameof(Index));
            }

            return View(role);
        }

        [Permission("Roles.Create")]
        public IActionResult Create()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} entered the Role Creation page.", currentUserId);

            var canAssignPermissions = currentUserId.HasValue && _permissionService
                .UserHasPermissionAsync(currentUserId.Value, "Roles.AssignPermissions")
                .GetAwaiter().GetResult();

            ViewBag.CanAssignPermissions = canAssignPermissions;
            ViewBag.Permissions = canAssignPermissions ? _context.Permissions.ToList() : new List<Domain.Entities.Permission.Permission>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Roles.Create")]
        public async Task<IActionResult> Create(Domain.Entities.Permission.Role role, List<int> SelectedPermissions)
        {
            ModelState.Remove("RolePermissions");
            ModelState.Remove("UserRoles");

            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} submitted a request to create a new role with Name: {RoleName}.", currentUserId, role?.RoleName);

            var canAssignPermissions = currentUserId.HasValue && await _permissionService.UserHasPermissionAsync(currentUserId.Value, "Roles.AssignPermissions");
            ViewBag.CanAssignPermissions = canAssignPermissions;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Role creation failed due to invalid ModelState for User {UserId}. Errors count: {ErrorCount}", currentUserId, ModelState.ErrorCount);
                ViewBag.Permissions = canAssignPermissions ? _context.Permissions.ToList() : new List<Domain.Entities.Permission.Permission>();
                return View(role);
            }

            // Check if role name already exists
            var existingRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName == role.RoleName);

            if (existingRole != null)
            {
                _logger.LogWarning("Role creation rejected. Role name '{RoleName}' already exists. Attempted by User {UserId}.", role.RoleName, currentUserId);
                ModelState.AddModelError("RoleName", "A role with this name already exists");
                ViewBag.Permissions = canAssignPermissions ? _context.Permissions.ToList() : new List<Domain.Entities.Permission.Permission>();
                return View(role);
            }

            // Set creation date
            role.CreateDate = DateTime.Now;

            // Add role to database
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Role '{RoleName}' successfully created with Generated ID: {RoleId} by User {UserId}.", role.RoleName, role.RoleId, currentUserId);

            // Add selected permissions only if user is allowed to assign permissions
            if (canAssignPermissions && SelectedPermissions != null && SelectedPermissions.Any())
            {
                _logger.LogInformation("Assigning {PermissionCount} permissions to newly created Role ID {RoleId} by User {UserId}.", SelectedPermissions.Count, role.RoleId, currentUserId);
                foreach (var permissionId in SelectedPermissions)
                {
                    _context.RolePermissions.Add(new Domain.Entities.Permission.RolePermission
                    {
                        RoleId = role.RoleId,
                        PermissionId = permissionId
                    });
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("Permissions successfully assigned to Role ID {RoleId}.", role.RoleId);
            }

            TempData["SuccessMessage"] = $"Role '{role.DisplayName}' created successfully";
            return RedirectToAction(nameof(Index));
        }

        [Permission("Roles.Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} requested Edit page for Role ID: {RoleId}.", currentUserId, id);

            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleId == id);

            if (role == null)
            {
                _logger.LogWarning("Role with ID {RoleId} not found for editing by User {UserId}.", id, currentUserId);
                TempData["ErrorMessage"] = "Role not found";
                return RedirectToAction(nameof(Index));
            }

            // Prevent editing SuperAdmin role
            if (role.RoleName == "SuperAdmin")
            {
                _logger.LogCritical("Security Warning: User {UserId} attempted to access Edit page for protected role 'SuperAdmin'. Access Denied.", currentUserId);
                TempData["ErrorMessage"] = "Access Denied: SuperAdmin role cannot be modified";
                return RedirectToAction(nameof(Index));
            }

            var canAssignPermissions = currentUserId.HasValue && await _permissionService.UserHasPermissionAsync(currentUserId.Value, "Roles.AssignPermissions");
            ViewBag.CanAssignPermissions = canAssignPermissions;

            ViewBag.Permissions = canAssignPermissions ? _context.Permissions.ToList() : new List<Domain.Entities.Permission.Permission>();
            ViewBag.RolePermissions = canAssignPermissions ? role.RolePermissions.Select(rp => rp.PermissionId).ToList() : new List<int>();

            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Roles.Edit")]
        public async Task<IActionResult> Edit(Domain.Entities.Permission.Role role, List<int> SelectedPermissions)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} submitted modifications for Role ID: {RoleId}.", currentUserId, role?.RoleId);

            var existingRole = await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleId == role.RoleId);

            if (existingRole == null)
            {
                _logger.LogWarning("Role with ID {RoleId} not found during Edit POST by User {UserId}.", role?.RoleId, currentUserId);
                TempData["ErrorMessage"] = "Role not found";
                return RedirectToAction(nameof(Index));
            }

            // Prevent modification of SuperAdmin role
            if (existingRole.RoleName == "SuperAdmin")
            {
                _logger.LogCritical("Security Violation: User {UserId} attempted to modify protected 'SuperAdmin' role via POST. Request Blocked.", currentUserId);
                TempData["ErrorMessage"] = "Access Denied: SuperAdmin role cannot be modified";
                return RedirectToAction(nameof(Index));
            }

            ModelState.Remove("RolePermissions");
            ModelState.Remove("UserRoles");

            var canAssignPermissions = currentUserId.HasValue && await _permissionService.UserHasPermissionAsync(currentUserId.Value, "Roles.AssignPermissions");
            ViewBag.CanAssignPermissions = canAssignPermissions;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Role compilation failed validation on Edit for Role ID {RoleId} by User {UserId}.", role.RoleId, currentUserId);
                ViewBag.Permissions = canAssignPermissions ? _context.Permissions.ToList() : new List<Domain.Entities.Permission.Permission>();
                ViewBag.RolePermissions = canAssignPermissions ? (SelectedPermissions ?? new List<int>()) : new List<int>();
                return View(role);
            }

            // Check if another role with same name exists
            var duplicateRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName == role.RoleName && r.RoleId != role.RoleId);

            if (duplicateRole != null)
            {
                _logger.LogWarning("Conflict detected. User {UserId} tried to rename Role ID {RoleId} to '{RoleName}', which is already taken by Role ID {DuplicateId}.", currentUserId, role.RoleId, role.RoleName, duplicateRole.RoleId);
                ModelState.AddModelError("RoleName", "A role with this name already exists");
                ViewBag.Permissions = canAssignPermissions ? _context.Permissions.ToList() : new List<Domain.Entities.Permission.Permission>();
                ViewBag.RolePermissions = canAssignPermissions ? (SelectedPermissions ?? new List<int>()) : new List<int>();
                return View(role);
            }

            _logger.LogInformation("Updating properties for Role ID {RoleId}. Old Name: '{OldName}', New Name: '{NewName}'", existingRole.RoleId, existingRole.RoleName, role.RoleName);

            // Update role properties
            existingRole.RoleName = role.RoleName;
            existingRole.DisplayName = role.DisplayName;
            existingRole.Description = role.Description;
            existingRole.IsActive = role.IsActive;

            // Update permissions only if current admin can assign permissions
            if (canAssignPermissions)
            {
                _logger.LogInformation("Syncing permissions for Role ID {RoleId} by User {UserId}. Cleared old assignments.", existingRole.RoleId, currentUserId);
                _context.RolePermissions.RemoveRange(existingRole.RolePermissions);

                if (SelectedPermissions != null && SelectedPermissions.Any())
                {
                    foreach (var permissionId in SelectedPermissions)
                    {
                        _context.RolePermissions.Add(new Domain.Entities.Permission.RolePermission
                        {
                            RoleId = existingRole.RoleId,
                            PermissionId = permissionId
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Role ID {RoleId} was successfully updated by User {UserId}.", existingRole.RoleId, currentUserId);

            TempData["SuccessMessage"] = $"Role '{existingRole.DisplayName}' updated successfully";
            return RedirectToAction(nameof(Details), new { id = existingRole.RoleId });
        }

        [HttpPost]
        [Permission("Roles.Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogWarning("User {UserId} initiated a DELETE request for Role ID: {RoleId}.", currentUserId, id);

            var role = await _context.Roles
                .Include(r => r.UserRoles)
                .FirstOrDefaultAsync(r => r.RoleId == id);

            if (role == null)
            {
                _logger.LogWarning("Delete failed. Role ID {RoleId} not found. Requested by User {UserId}.", id, currentUserId);
                return Json(new { success = false, message = "Role not found" });
            }

            // Check if role is assigned to any users
            if (role.UserRoles.Any())
            {
                _logger.LogWarning("Delete rejected. Role '{RoleName}' (ID: {RoleId}) is currently assigned to {Count} users. Action requested by User {UserId}.", role.RoleName, id, role.UserRoles.Count, currentUserId);
                return Json(new { success = false, message = "Cannot delete role that is assigned to users" });
            }

            // Prevent deletion of SuperAdmin role
            if (role.RoleName == "SuperAdmin")
            {
                _logger.LogCritical("Security Breach Attempt: User {UserId} attempted to delete 'SuperAdmin' role. Action blocked.", currentUserId);
                return Json(new { success = false, message = "Cannot delete SuperAdmin role" });
            }

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Role '{RoleName}' (ID: {RoleId}) successfully deleted by User {UserId}.", role.RoleName, id, currentUserId);

            return Json(new { success = true, message = "Role deleted successfully" });
        }

        [HttpPost]
        [Permission("Roles.Edit")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} requested ToggleActive for Role ID: {RoleId}.", currentUserId, id);

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == id);

            if (role == null)
            {
                _logger.LogWarning("ToggleActive failed. Role ID {RoleId} not found. Requested by User {UserId}.", id, currentUserId);
                return Json(new { success = false, message = "Role not found" });
            }

            // Prevent deactivation of SuperAdmin role
            if (role.RoleName == "SuperAdmin")
            {
                _logger.LogCritical("Security Warning: User {UserId} attempted to change active status of 'SuperAdmin' role. Action blocked.", currentUserId);
                return Json(new { success = false, message = "Cannot deactivate SuperAdmin role" });
            }

            role.IsActive = !role.IsActive;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Role ID {RoleId} active status toggled to {IsActive} by User {UserId}.", id, role.IsActive, currentUserId);

            return Json(new
            {
                success = true,
                isActive = role.IsActive,
                message = role.IsActive ? "Role activated" : "Role deactivated"
            });
        }
    }
}