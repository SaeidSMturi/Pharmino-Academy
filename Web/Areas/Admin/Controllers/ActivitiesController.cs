using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    public class ActivitiesController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly ILogger<ActivitiesController> _logger;

        // تزریق ILogger به کانستراکتور
        public ActivitiesController(WebContext context, ILogger<ActivitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Permission("Dashboard.View")]
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Fetching recent activities for the admin dashboard.");

            try
            {
                var activities = await _context.RecentActivities
                    .Include(a => a.RecentActivityPriority)
                    .OrderByDescending(a => a.CreateDate)
                    .Take(100)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} recent activities.", activities.Count);
                return View(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching recent activities.");
                // بسته به سیاست پروژه می‌توانید خطا را Re-throw کنید یا کاربر را به یک صفحه خطا هدایت کنید
                throw;
            }
        }

        [HttpPost]
        [Permission("Dashboard.View")]
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Attempting to delete activity with ID: {ActivityId}", id);

            try
            {
                var activity = await _context.RecentActivities.FirstOrDefaultAsync(a => a.Id == id);
                if (activity == null)
                {
                    _logger.LogWarning("Delete failed. Activity with ID {ActivityId} was not found.", id);
                    return Json(new { success = false, message = "Activity not found" });
                }

                _context.RecentActivities.Remove(activity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted activity with ID: {ActivityId}", id);
                return Json(new { success = true, message = "Activity deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting activity with ID {ActivityId}.", id);
                return Json(new { success = false, message = "An internal error occurred while deleting the activity." });
            }
        }

        [HttpPost]
        [Permission("Dashboard.View")]
        public async Task<IActionResult> ClearAll()
        {
            _logger.LogWarning("Request received to clear all recent activities.");

            try
            {
                var activities = await _context.RecentActivities.ToListAsync();
                int count = activities.Count;

                if (count == 0)
                {
                    _logger.LogInformation("ClearAll invoked, but there are no activities to clear.");
                    return Json(new { success = true, message = "No activities to clear" });
                }

                _context.RecentActivities.RemoveRange(activities);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully cleared all recent activities. Total removed: {Count}", count);
                return Json(new { success = true, message = "All activities cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while clearing all recent activities.");
                return Json(new { success = false, message = "An internal error occurred while clearing activities." });
            }
        }
    }
}