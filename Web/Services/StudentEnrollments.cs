using System.Security.Claims;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Web.Services
{
    /// <summary>
    /// The courses the signed-in user owns: bought directly, or included in a bundle they bought
    /// (the same rule as CourseService.IsUserEnrolledAsync). Loaded once per request, so every course
    /// card on a page can show "Enrolled" without a query each.
    /// </summary>
    public sealed class StudentEnrollments
    {
        private readonly WebContext _context;
        private HashSet<int>? _courseIds;

        public StudentEnrollments(WebContext context)
        {
            _context = context;
        }

        public async Task<HashSet<int>> GetCourseIdsAsync(ClaimsPrincipal? user)
        {
            if (_courseIds != null) return _courseIds;

            if (user?.Identity?.IsAuthenticated != true ||
                !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return _courseIds = new HashSet<int>();

            var direct = await _context.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => e.CourseId)
                .ToListAsync();

            var viaBundles = direct.Count == 0
                ? new List<int>()
                : await _context.CourseBundleItems
                    .AsNoTracking()
                    .Where(b => direct.Contains(b.BundleId))
                    .Select(b => b.CourseId)
                    .ToListAsync();

            return _courseIds = direct.Concat(viaBundles).ToHashSet();
        }
    }
}
