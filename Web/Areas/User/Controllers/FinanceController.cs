using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class FinanceController : Controller
    {
        private readonly IApplicationDbContext _context;

        public FinanceController(IApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var uid) ? uid : 0;
        }

        // GET: /User/Finance
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();

            var transactions = await _context.PaymentTransactions
                .Include(t => t.Course)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreateDate)
                .ToListAsync();

            return View(transactions);
        }
    }
}
