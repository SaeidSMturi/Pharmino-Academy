using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    /// <summary>
    /// Base controller for all Admin area controllers.
    /// Ensures that only authenticated users with at least one role can access the Admin area.
    /// Users without any roles will receive a 404 Not Found response.
    /// </summary>
    [Area("Admin")]
    [Authorize]
    [RequireRole]
    public abstract class AdminBaseController : Controller
    {
    }
}
