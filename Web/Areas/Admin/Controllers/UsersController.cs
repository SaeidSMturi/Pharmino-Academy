using Web.Extensions; // Added for GetUserId extension
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web.Areas.Admin.ViewModels;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    public class UsersController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IPermissionService _permissionService;
        private readonly ISuspiciousActivityService _suspiciousActivityService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            WebContext context,
            IPasswordHasher passwordHasher,
            IPermissionService permissionService,
            ISuspiciousActivityService suspiciousActivityService,
            ILogger<UsersController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _permissionService = permissionService;
            _suspiciousActivityService = suspiciousActivityService;
            _logger = logger;
        }

        [Permission("Users.Students.View")]
        public async Task<IActionResult> Students()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("Admin user {UserId} is fetching the list of Students.", currentUserId);

            var users = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles == null || !u.UserRoles.Any())
                .OrderByDescending(u => u.RegisterDate)
                .ToListAsync();

            _logger.LogInformation("Successfully retrieved {Count} student accounts.", users.Count);
            ViewData["Title"] = "Students";
            return View("Index", users);
        }

        [Permission("Users.Admin.View")]
        public async Task<IActionResult> AdminUsers()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("Admin user {UserId} is fetching the list of Administrative/Staff Users.", currentUserId);

            var users = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles != null && u.UserRoles.Any())
                .OrderByDescending(u => u.RegisterDate)
                .ToListAsync();

            _logger.LogInformation("Successfully retrieved {Count} administrative user accounts.", users.Count);
            ViewData["Title"] = "Admin Users";
            return View("Index", users);
        }

        [Permission("Users.View")]
        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} requested detailed profile information for Target User ID: {TargetUserId}.", currentUserId, id);

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                _logger.LogWarning("Details request failed. Target User ID {TargetUserId} not found. Requested by Admin {UserId}.", id, currentUserId);
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Students));
            }

            _logger.LogInformation("Fetching suspicious activities and lock logs for Target User ID: {TargetUserId}.", id);

            // دریافت تاریخچه رفتارهای مشکوک
            var suspiciousActivities = await _suspiciousActivityService
                .GetUserSuspiciousActivitiesAsync(id, 1, 50);

            // دریافت تاریخچه قفل و باز شدن
            var lockHistory = await _suspiciousActivityService
                .GetAccountLockHistoryAsync(id);

            ViewBag.SuspiciousActivities = suspiciousActivities;
            ViewBag.LockHistory = lockHistory;

            // دریافت دوره‌های ثبت‌نام‌شده کاربر
            var enrollments = await _context.CourseEnrollments
                .Include(e => e.Course)
                .Where(e => e.UserId == id)
                .OrderByDescending(e => e.EnrollmentDate)
                .ToListAsync();

            // تمام دوره‌ها برای modal افزودن ثبت‌نام
            var allCourses = await _context.Courses
                .OrderBy(c => c.Title)
                .ToListAsync();

            ViewBag.UserEnrollments = enrollments;
            ViewBag.AllCourses = allCourses;

            return View(user);
        }

        [Permission("Users.Create")]
        public IActionResult Create()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} opened the User Creation form.", currentUserId);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Users.Create")]
        public async Task<IActionResult> Create(CreateUserViewModel viewModel)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} submitted data to create user: '{UserName}' (Email: {Email}).", currentUserId, viewModel?.UserName, viewModel?.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("User creation form failed model validation for Admin {UserId}. Errors: {ErrorCount}", currentUserId, ModelState.ErrorCount);
                return View(viewModel);
            }

            // Check if username already exists
            var existingUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == viewModel.UserName);
            if (existingUsername != null)
            {
                _logger.LogWarning("User creation rejected. Username '{UserName}' already exists. Attempted by Admin {UserId}.", viewModel.UserName, currentUserId);
                ModelState.AddModelError("UserName", "This username is already taken");
                return View(viewModel);
            }

            // Check if email already exists
            var existingEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == viewModel.Email);
            if (existingEmail != null)
            {
                _logger.LogWarning("User creation rejected. Email '{Email}' is already registered. Attempted by Admin {UserId}.", viewModel.Email, currentUserId);
                ModelState.AddModelError("Email", "This email is already registered");
                return View(viewModel);
            }

            // Check if phone number already exists
            var existingPhone = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == viewModel.PhoneNumber);
            if (existingPhone != null)
            {
                _logger.LogWarning("User creation rejected. Phone number '{PhoneNumber}' is already registered. Attempted by Admin {UserId}.", viewModel.PhoneNumber, currentUserId);
                ModelState.AddModelError("PhoneNumber", "This phone number is already registered");
                return View(viewModel);
            }

            // Create new user
            var user = new Domain.Entities.User.User
            {
                UserName = viewModel.UserName,
                FirstName = viewModel.FirstName,
                LastName = viewModel.LastName,
                Email = viewModel.Email,
                PhoneNumber = viewModel.PhoneNumber,
                Password = _passwordHasher.HashPassword(viewModel.Password),
                IsEmailActive = viewModel.IsEmailActive,
                IsPhoneNumberActive = viewModel.IsPhoneNumberActive,
                IsBan = viewModel.IsBan,
                ActiveCode = Guid.NewGuid().ToString().Replace("-", ""),
                SecurityCode = Guid.NewGuid().ToString().Replace("-", ""),
                ActivePhoneNumberCode = new Random().Next(10000, 99999).ToString(),
                RegisterDate = DateTime.Now,
                LastChange = DateTime.Now,
                IsDelete = false,
                TryCount = 0,
                LoginWithGoogle = false
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT: User '{UserName}' successfully created with assigned ID {TargetUserId} by Admin {UserId}.", user.UserName, user.UserId, currentUserId);

            TempData["SuccessMessage"] = "User created successfully";
            return RedirectToAction(nameof(Students));
        }

        [Permission("Users.Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} requested Edit form for Target User ID: {TargetUserId}.", currentUserId, id);

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                _logger.LogWarning("Edit form request failed. User ID {TargetUserId} not found. Action by User {UserId}.", id, currentUserId);
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Students));
            }

            // Prevent editing Admin user
            if (user.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogCritical("SECURITY WARNING: User {UserId} attempted to open the Edit form for the protected 'Admin' master account. Request Denied.", currentUserId);
                TempData["ErrorMessage"] = "Access Denied: Admin user cannot be modified";
                return RedirectToAction(nameof(Students));
            }

            // Determine if current admin can assign roles
            var canAssignRoles = currentUserId.HasValue && await _permissionService.UserHasPermissionAsync(currentUserId.Value, "Users.AssignRoles");

            List<RoleSelectionViewModel> availableRoles = new();
            List<int> userRoleIds = new();

            if (canAssignRoles)
            {
                userRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
                var allRoles = await _context.Roles
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.DisplayName)
                    .ToListAsync();

                availableRoles = allRoles.Select(r => new RoleSelectionViewModel
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    DisplayName = r.DisplayName,
                    Description = r.Description,
                    IsSelected = userRoleIds.Contains(r.RoleId)
                }).ToList();
            }

            var viewModel = new EditUserViewModel
            {
                UserId = user.UserId,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsEmailActive = user.IsEmailActive,
                IsPhoneNumberActive = user.IsPhoneNumberActive,
                IsBan = user.IsBan,
                SelectedRoleIds = canAssignRoles ? userRoleIds : new List<int>(),
                AvailableRoles = canAssignRoles ? availableRoles : new List<RoleSelectionViewModel>()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Users.Edit")]
        public async Task<IActionResult> Edit(EditUserViewModel viewModel)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} submitted modifications for Target User ID: {TargetUserId}.", currentUserId, viewModel?.UserId);

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.UserId == viewModel.UserId);

            if (user == null)
            {
                _logger.LogWarning("Edit POST failed. User ID {TargetUserId} not found. Action by Admin {UserId}.", viewModel?.UserId, currentUserId);
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Students));
            }

            // Prevent modification of Admin user
            if (user.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogCritical("SECURITY VIOLATION: User {UserId} attempted to modify the protected 'Admin' master account via POST. Request Blocked.", currentUserId);
                TempData["ErrorMessage"] = "Access Denied: Admin user cannot be modified";
                return RedirectToAction(nameof(Students));
            }

            var canAssignRoles = currentUserId.HasValue && await _permissionService.UserHasPermissionAsync(currentUserId.Value, "Users.AssignRoles");

            if (viewModel.NewPassword != null)
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("User edit failed validation for Target User ID {TargetUserId} by Admin {UserId}.", viewModel.UserId, currentUserId);
                    if (canAssignRoles)
                    {
                        await LoadAvailableRoles(viewModel);
                    }
                    else
                    {
                        viewModel.SelectedRoleIds = new List<int>();
                        viewModel.AvailableRoles = new List<RoleSelectionViewModel>();
                    }
                    return View(viewModel);
                }

                // Check if username already exists for another user
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == viewModel.UserName && u.UserId != viewModel.UserId);
                if (existingUser != null)
                {
                    _logger.LogWarning("User edit rejected. Username '{UserName}' is already taken by another user. Changed by Admin {UserId}.", viewModel.UserName, currentUserId);
                    ModelState.AddModelError("UserName", "This username is already taken");
                    if (canAssignRoles) { await LoadAvailableRoles(viewModel); }
                    else
                    {
                        viewModel.SelectedRoleIds = new List<int>();
                        viewModel.AvailableRoles = new List<RoleSelectionViewModel>();
                    }
                    return View(viewModel);
                }

                // Prevent changing username to "Admin"
                if (viewModel.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                    !user.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogCritical("SECURITY ALERT: User {UserId} attempted to rename user ID {TargetUserId} to reserved name 'Admin'. Action blocked.", currentUserId, viewModel.UserId);
                    ModelState.AddModelError("UserName", "Cannot use 'Admin' as username - it is reserved");
                    if (canAssignRoles) { await LoadAvailableRoles(viewModel); }
                    else
                    {
                        viewModel.SelectedRoleIds = new List<int>();
                        viewModel.AvailableRoles = new List<RoleSelectionViewModel>();
                    }
                    return View(viewModel);
                }

                _logger.LogInformation("AUDIT: Updating user information for Target User ID {TargetUserId}. Old Username: '{OldUser}', New Username: '{NewUser}'", user.UserId, user.UserName, viewModel.UserName);

                // Update user properties
                user.UserName = viewModel.UserName;
                user.FirstName = viewModel.FirstName;
                user.LastName = viewModel.LastName;
                user.Email = viewModel.Email;
                user.PhoneNumber = viewModel.PhoneNumber;
                user.IsEmailActive = viewModel.IsEmailActive;
                user.IsPhoneNumberActive = viewModel.IsPhoneNumberActive;
                user.IsBan = viewModel.IsBan;
                user.LastChange = DateTime.Now;

                // Update password if provided
                if (!string.IsNullOrEmpty(viewModel.NewPassword))
                {
                    _logger.LogWarning("AUDIT: Password hash updated for Target User ID {TargetUserId} by Admin {UserId}.", user.UserId, currentUserId);
                    user.Password = _passwordHasher.HashPassword(viewModel.NewPassword);
                }
            }

            // Update user roles ONLY if current admin has Users.AssignRoles permission
            if (canAssignRoles)
            {
                _logger.LogInformation("Admin {UserId} is syncing roles for Target User ID {TargetUserId}.", currentUserId, user.UserId);
                await UpdateUserRoles(user.UserId, viewModel.SelectedRoleIds ?? new List<int>());
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("AUDIT SUCCESS: User ID {TargetUserId} was successfully updated by Admin {UserId}.", user.UserId, currentUserId);

            TempData["SuccessMessage"] = "User information updated successfully";

            if (await _permissionService.UserHasAnyRoleAsync(user.UserId))
                return RedirectToAction(nameof(AdminUsers));
            else
                return RedirectToAction(nameof(Students));
        }

        [HttpPost]
        [Permission("Users.Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogWarning("AUDIT: User {UserId} initiated a DELETE request for Target User ID: {TargetUserId}.", currentUserId, id);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
            {
                _logger.LogWarning("Delete failed. Target User ID {TargetUserId} not found. Action by Admin {UserId}.", id, currentUserId);
                return Json(new { success = false, message = "User not found" });
            }

            // Prevent deletion of Admin user
            if (user.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogCritical("SECURITY BREACH ATTEMPT: User {UserId} attempted to delete protected master 'Admin' user. Action Blocked.", currentUserId);
                return Json(new { success = false, message = "Access Denied: Admin user cannot be deleted" });
            }

            user.IsDelete = true;
            await _context.SaveChangesAsync();
            _logger.LogInformation("AUDIT SUCCESS: Target User '{UserName}' (ID: {TargetUserId}) was soft-deleted by Admin {UserId}.", user.UserName, id, currentUserId);

            return Json(new { success = true, message = "User deleted successfully" });
        }

        [HttpPost]
        [Permission("Users.Ban")]
        public async Task<IActionResult> ToggleBan([FromBody] ToggleBanRequest request)
        {
            var currentUserId = User.GetUserId();
            _logger.LogWarning("AUDIT: User {UserId} requested ToggleBan for Target User ID: {TargetUserId}.", currentUserId, request?.Id);

            if (request == null || request.Id <= 0)
            {
                _logger.LogWarning("ToggleBan rejected. Invalid request body submitted by Admin {UserId}.", currentUserId);
                return Json(new { success = false, message = "Invalid request" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.Id);
            if (user == null)
            {
                _logger.LogWarning("ToggleBan failed. Target User ID {TargetUserId} not found. Action by Admin {UserId}.", request.Id, currentUserId);
                return Json(new { success = false, message = "User not found" });
            }

            // Prevent banning Admin user
            if (user.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogCritical("SECURITY BREACH ATTEMPT: User {UserId} attempted to BAN the protected master 'Admin' user. Action Blocked.", currentUserId);
                return Json(new { success = false, message = "Access Denied: Admin user cannot be banned" });
            }

            user.IsBan = !user.IsBan;
            await _context.SaveChangesAsync();
            _logger.LogInformation("AUDIT SUCCESS: Target User '{UserName}' (ID: {TargetUserId}) ban status toggled to {IsBan} by Admin {UserId}.", user.UserName, user.UserId, user.IsBan, currentUserId);

            return Json(new { success = true, isBan = user.IsBan, message = user.IsBan ? "User banned" : "User activated" });
        }

        private async Task LoadAvailableRoles(EditUserViewModel viewModel)
        {
            var allRoles = await _context.Roles
                .Where(r => r.IsActive)
                .OrderBy(r => r.DisplayName)
                .ToListAsync();

            viewModel.AvailableRoles = allRoles.Select(r => new RoleSelectionViewModel
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                DisplayName = r.DisplayName,
                Description = r.Description,
                IsSelected = viewModel.SelectedRoleIds?.Contains(r.RoleId) ?? false
            }).ToList();
        }

        private async Task UpdateUserRoles(int userId, List<int> selectedRoleIds)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null && user.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("UpdateUserRoles skipped. Modification of master 'Admin' roles is strictly restricted.");
                return;
            }

            var superAdminRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName == "SuperAdmin");

            var currentUserRoles = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .ToListAsync();

            var rolesToRemove = currentUserRoles
                .Where(ur => !selectedRoleIds.Contains(ur.RoleId) &&
                            (superAdminRole == null || ur.RoleId != superAdminRole.RoleId))
                .ToList();

            if (rolesToRemove.Any())
            {
                _logger.LogInformation("Removing {Count} roles from User ID {TargetUserId}.", rolesToRemove.Count, userId);
                _context.UserRoles.RemoveRange(rolesToRemove);
            }

            var currentRoleIds = currentUserRoles.Select(ur => ur.RoleId).ToList();
            var rolesToAdd = selectedRoleIds
                .Where(roleId => !currentRoleIds.Contains(roleId) &&
                                (superAdminRole == null || roleId != superAdminRole.RoleId))
                .Select(roleId => new Domain.Entities.Permission.UserRole
                {
                    UserId = userId,
                    RoleId = roleId,
                    AssignedDate = DateTime.Now,
                    AssignedBy = null
                })
                .ToList();

            if (rolesToAdd.Any())
            {
                _logger.LogInformation("Assigning {Count} new roles to User ID {TargetUserId}.", rolesToAdd.Count, userId);
                await _context.UserRoles.AddRangeAsync(rolesToAdd);
            }
        }
    }
    public class ToggleBanRequest
    {
        public int Id { get; set; }
    }
}