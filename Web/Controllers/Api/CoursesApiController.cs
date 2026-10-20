using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers.Api
{
    [ApiController]
    [Route("api/courses")]
    public class CoursesApiController : ControllerBase
    {
        private readonly ICourseService _courseService;
        private readonly ILogger<CoursesApiController> _logger;

        public CoursesApiController(ICourseService courseService, ILogger<CoursesApiController> logger)
        {
            _courseService = courseService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/courses
        /// Returns all active courses for Pharmino Academy mobile app
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCourses()
        {
            try
            {
                var courses = await _courseService.GetActiveCoursesAsync();
                var result = courses.Select(c => new
                {
                    courseId = c.CourseId,
                    title = c.Title,
                    description = c.Description,
                    price = c.Price,
                    durationHours = c.DurationHours ?? 0,
                    imageName = c.ImageName,
                    videoCount = c.Videos.Count(v => v.IsActive),
                    isActive = c.IsActive
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile API: Error fetching courses list.");
                return StatusCode(500, new { message = "Error loading courses" });
            }
        }

        /// <summary>
        /// GET /api/courses/{id}
        /// Returns details and video curriculum for a specific course
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCourseDetails(int id)
        {
            try
            {
                var course = await _courseService.GetCourseWithDetailsAsync(id);
                if (course == null)
                    return NotFound(new { message = "Course not found" });

                var videos = await _courseService.GetCourseVideosAsync(id, 0);

                var result = new
                {
                    courseId = course.CourseId,
                    title = course.Title,
                    description = course.Description,
                    price = course.Price,
                    durationHours = course.DurationHours ?? 0,
                    imageName = course.ImageName,
                    videoCount = videos.Count,
                    isActive = course.IsActive,
                    videos = videos.Select(v => new
                    {
                        videoId = v.VideoId,
                        courseId = v.CourseId,
                        title = v.Title,
                        description = v.Description,
                        videoFileName = v.VideoFileName,
                        durationSeconds = v.DurationSeconds ?? 0,
                        orderIndex = v.OrderIndex,
                        isFree = v.IsFree,
                        isActive = v.IsActive
                    })
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile API: Error fetching course details for ID: {CourseId}", id);
                return StatusCode(500, new { message = "Error loading course details" });
            }
        }
    }
}
