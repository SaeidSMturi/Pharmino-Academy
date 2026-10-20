using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    [Route("Admin/logs")]
    public class LogsController : AdminBaseController
    {
        private readonly ILogManagerService _logManagerService;

        public LogsController(ILogManagerService logManagerService)
        {
            _logManagerService = logManagerService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // دریافت خلاصه وضعیت لاگ‌ها، شمارش خطاها و لیست فایل‌ها
            var summary = await _logManagerService.GetLogSummaryAsync();
            return View(summary);
        }

        [HttpGet("view-raw")]
        public async Task<IActionResult> ViewRaw([FromQuery] string filePath)
        {
            try
            {
                var content = await _logManagerService.GetRawLogFileContentAsync(filePath);
                return Content(content, "text/plain");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("clear-all")]
        public async Task<IActionResult> ClearAllLogs()
        {
            var result = await _logManagerService.ClearAllLogsAsync();

            if (result)
            {
                return Ok(new { success = true, message = "All logs were successfully cleared." });
            }

            return BadRequest(new { success = false, message = "Error clearing log files. Some files may be involved." });
        }
    }
}
