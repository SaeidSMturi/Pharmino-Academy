using Web.Extensions;
using Infrastructure.Services;
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class SettingsController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            WebContext context,
            IWebHostEnvironment env,
            ISettingsService settingsService,
            ILogger<SettingsController> logger)
        {
            _context = context;
            _env = env;
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("Admin user {UserId} is fetching application settings and system configurations.", currentUserId);

            var settings = await _settingsService.GetAllSettingsAsync();
            var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
            string appSettingsContent = "{}";

            if (System.IO.File.Exists(appSettingsPath))
            {
                appSettingsContent = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                _logger.LogInformation("Successfully read 'appsettings.json' configuration file from disk.");
            }
            else
            {
                _logger.LogWarning("'appsettings.json' file not found at expected path: {FilePath}. Falling back to empty object.", appSettingsPath);
            }

            ViewBag.AppSettingsContent = appSettingsContent;

            try
            {
                var options = new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var parsedJson = JsonDocument.Parse(appSettingsContent, options);
                ViewBag.ParsedJson = parsedJson;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse 'appsettings.json' content on Index view request.");
                ViewBag.ParsedJson = null;
            }

            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSetting(int id, string? dataString, bool? dataBool, int? dataInt, decimal? dataDecimal)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} requested updating Setting ID: {SettingId}.", currentUserId, id);

            var setting = await _context.Settings.FirstOrDefaultAsync(s => s.Id == id);
            if (setting == null)
            {
                _logger.LogWarning("Update rejected. Setting ID {SettingId} not found. Requested by User {UserId}.", id, currentUserId);
                TempData["ErrorMessage"] = "Setting not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!setting.Changeable)
            {
                _logger.LogWarning("Security/Policy Alert: User {UserId} attempted to modify a non-changeable system setting: '{SettingTitle}' (ID: {SettingId}).", currentUserId, setting.Title, id);
                TempData["ErrorMessage"] = "This setting cannot be modified.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("AUDIT: Updating Setting '{SettingTitle}' (ID: {SettingId}). Values before change -> String: '{OldString}', Bool: '{OldBool}', Int: '{OldInt}', Decimal: '{OldDecimal}'",
                setting.Title, id, setting.DataString, setting.DataBool, setting.DataInt, setting.DataDecimal);

            setting.DataString = dataString;
            setting.DataBool = dataBool;
            setting.DataInt = dataInt;
            setting.DataDecimal = dataDecimal;
            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Setting '{SettingTitle}' (ID: {SettingId}) updated in database successfully by User {UserId}.", setting.Title, id, currentUserId);

            await _settingsService.InvalidateCacheAsync();
            _logger.LogInformation("Application settings cache invalidated successfully.");

            TempData["SuccessMessage"] = $"Setting '{setting.Title}' has been updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateJsonField(string jsonPath, string newValue)
        {
            var currentUserId = User.GetUserId();
            _logger.LogWarning("CRITICAL ACTIONS: User {UserId} is attempting to update a single key directly inside 'appsettings.json'. Path: '{JsonPath}', Target Value: '{NewValue}'", currentUserId, jsonPath, newValue);

            if (string.IsNullOrEmpty(jsonPath))
            {
                _logger.LogWarning("UpdateJsonField aborted. The provided JSON path was null or empty.");
                return RedirectToAction(nameof(Index));
            }

            var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
            if (!System.IO.File.Exists(appSettingsPath))
            {
                _logger.LogError("Configuration file target not found on server disk. Path: {FilePath}", appSettingsPath);
                TempData["ErrorMessage"] = "Configuration file not found on server.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await CreateSnapshotAndCleanupAsync();

                var jsonContent = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                var jsonNode = JsonNode.Parse(jsonContent);

                string[] pathParts = jsonPath.Split(':');
                JsonNode currentNode = jsonNode;

                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    currentNode = currentNode[pathParts[i]];
                }

                var targetKey = pathParts[^1];
                var oldValue = currentNode[targetKey]?.ToString();

                if (bool.TryParse(newValue, out bool boolValue))
                {
                    currentNode[targetKey] = boolValue;
                }
                else if (int.TryParse(newValue, out int intValue))
                {
                    currentNode[targetKey] = intValue;
                }
                else if (decimal.TryParse(newValue, out decimal decimalValue))
                {
                    currentNode[targetKey] = decimalValue;
                }
                else
                {
                    currentNode[targetKey] = newValue;
                }

                var writeOptions = new JsonSerializerOptions { WriteIndented = true };
                await System.IO.File.WriteAllTextAsync(appSettingsPath, jsonNode.ToJsonString(writeOptions));

                _logger.LogInformation("AUDIT: 'appsettings.json' modified by User {UserId} at path '{JsonPath}'. Value changed from '{OldValue}' to '{NewValue}'.", currentUserId, jsonPath, oldValue, newValue);
                TempData["SuccessMessage"] = $"Configuration field '{jsonPath}' has been updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while User {UserId} was updating JSON path '{JsonPath}' in 'appsettings.json'.", currentUserId, jsonPath);
                TempData["ErrorMessage"] = $"Error updating configuration: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SaveAppSettings(string jsonContent)
        {
            var currentUserId = User.GetUserId();
            _logger.LogWarning("CRITICAL AUDIT: User {UserId} submitted a complete rewrite request for 'appsettings.json'.", currentUserId);

            if (string.IsNullOrEmpty(jsonContent))
            {
                _logger.LogWarning("Rewrite request rejected for User {UserId}. Submitted JSON content was empty.", currentUserId);
                TempData["ErrorMessage"] = "JSON content cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
                using var jsonDoc = JsonDocument.Parse(jsonContent, options);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Rewrite request failed. Invalid JSON format submitted by User {UserId}.", currentUserId);
                TempData["ErrorMessage"] = $"Invalid JSON format: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await CreateSnapshotAndCleanupAsync();

                var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                await System.IO.File.WriteAllTextAsync(appSettingsPath, jsonContent);

                _logger.LogCritical("AUDIT SUCCESS: 'appsettings.json' was completely OVERWRITTEN and saved successfully by User {UserId}.", currentUserId);
                TempData["SuccessMessage"] = "Configuration file has been saved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical Exception occurred while User {UserId} tried to completely overwrite 'appsettings.json'.", currentUserId);
                TempData["ErrorMessage"] = $"Error saving configuration: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task CreateSnapshotAndCleanupAsync()
        {
            var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
            if (!System.IO.File.Exists(appSettingsPath))
            {
                _logger.LogWarning("Snapshot skip: original 'appsettings.json' file does not exist.");
                return;
            }

            var snapshotDirectory = Path.Combine(_env.ContentRootPath, "App_Data", "AppSettingsSnapshots");
            if (!Directory.Exists(snapshotDirectory))
            {
                Directory.CreateDirectory(snapshotDirectory);
                _logger.LogInformation("Created snapshot directory at path: {DirectoryPath}", snapshotDirectory);
            }

            var snapshotFileName = $"appsettings.snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var snapshotPath = Path.Combine(snapshotDirectory, snapshotFileName);
            System.IO.File.Copy(appSettingsPath, snapshotPath, true);
            _logger.LogInformation("Backup snapshot successfully created: '{SnapshotName}'", snapshotFileName);

            try
            {
                var files = Directory.GetFiles(snapshotDirectory, "appsettings.snapshot_*.json");
                int deletionCount = 0;
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc < DateTime.UtcNow.AddDays(-30))
                    {
                        fileInfo.Delete();
                        deletionCount++;
                    }
                }
                if (deletionCount > 0)
                {
                    _logger.LogInformation("Snapshot cleanup task finished. Purged {Count} outdated snapshot file(s) (>30 days old).", deletionCount);
                }
            }
            catch (Exception ex)
            {
                // Suppress cleanup errors to prevent affecting main thread but log it.
                _logger.LogWarning(ex, "Background snapshot retention cleanup encountered an error but was suppressed.");
            }

            await Task.CompletedTask;
        }
    }
}