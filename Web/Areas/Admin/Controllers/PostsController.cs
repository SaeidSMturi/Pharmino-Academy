using Infrastructure.Persistence;
using Domain.Entities.Post;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class PostsController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PostsController> _logger;

        // Helper DTO to accept JSON body { id: N } or { Id: N }
        public class IdRequest
        {
            public int id { get; set; }
            public int Id { get; set; }
        }

        // تزریق ILogger به کانستراکتور
        public PostsController(WebContext context, IWebHostEnvironment environment, ILogger<PostsController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        [Permission("Dashboard.View")]
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Admin requested post list index.");
            try
            {
                var posts = await _context.Posts
                    .Include(p => p.User)
                    .OrderByDescending(p => p.CreateDate)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} posts for admin index.", posts.Count);
                return View(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching posts for admin index.");
                throw;
            }
        }

        [Permission("Dashboard.View")]
        public IActionResult Create()
        {
            _logger.LogInformation("Navigating to post creation page.");
            return View();
        }

        [Permission("Dashboard.View")]
        public async Task<IActionResult> Edit(int id)
        {
            _logger.LogInformation("Navigating to edit page for Post ID: {PostId}", id);

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.PostId == id);
            if (post == null)
            {
                _logger.LogWarning("Edit view request failed. Post with ID {PostId} was not found.", id);
                TempData["ErrorMessage"] = "Post not found";
                return RedirectToAction(nameof(Index));
            }

            return View(post);
        }

        [Permission("Dashboard.View")]
        public async Task<IActionResult> Details(int id)
        {
            _logger.LogInformation("Fetching details for Post ID: {PostId}", id);

            var post = await _context.Posts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                _logger.LogWarning("Details view request failed. Post with ID {PostId} was not found.", id);
                TempData["ErrorMessage"] = "Post not found";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Successfully retrieved details for Post ID: {PostId}. Author ID: {AuthorId}", id, post.AuthorId);
            return View(post);
        }

        [HttpPost]
        [Permission("Dashboard.View")]
        public async Task<IActionResult> Delete([FromBody] IdRequest? payload, int? id)
        {
            var postId = payload?.Id > 0 ? payload!.Id : (payload?.id > 0 ? payload!.id : (id ?? 0));
            _logger.LogWarning("Delete request received for Post ID: {PostId} by User: {Username}", postId, User.Identity?.Name ?? "Unknown");

            if (postId <= 0)
            {
                _logger.LogWarning("Delete aborted. Invalid Post ID provided: {PostId}", postId);
                return Json(new { success = false, message = "Invalid post id" });
            }

            try
            {
                var post = await _context.Posts.FirstOrDefaultAsync(p => p.PostId == postId);
                if (post == null)
                {
                    _logger.LogWarning("Delete failed. Post with ID {PostId} not found in database.", postId);
                    return Json(new { success = false, message = "Post not found" });
                }

                // در صورت تمایل به حذف فیزیکی تصویر کاور هنگام حذف پست، می‌توانید منطق آن را اینجا اضافه کنید.
                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted Post ID: {PostId} from database.", postId);
                return Json(new { success = true, message = "Post deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting Post ID: {PostId}", postId);
                return Json(new { success = false, message = "Delete failed internally" });
            }
        }

        [HttpPost]
        [Permission("Dashboard.View")]
        public async Task<IActionResult> TogglePublish([FromBody] IdRequest? payload, int? id)
        {
            var postId = payload?.Id > 0 ? payload!.Id : (payload?.id > 0 ? payload!.id : (id ?? 0));
            _logger.LogInformation("TogglePublish status change triggered for Post ID: {PostId}", postId);

            if (postId <= 0)
            {
                _logger.LogWarning("TogglePublish aborted due to invalid Post ID: {PostId}", postId);
                return Json(new { success = false, message = "Invalid post id" });
            }

            try
            {
                var post = await _context.Posts.FirstOrDefaultAsync(p => p.PostId == postId);
                if (post == null)
                {
                    _logger.LogWarning("TogglePublish failed. Post ID {PostId} not found.", postId);
                    return Json(new { success = false, message = "Post not found" });
                }

                post.IsPublish = !post.IsPublish;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Post ID {PostId} publish status updated to: {IsPublish}", postId, post.IsPublish);
                return Json(new { success = true, isPublish = post.IsPublish, message = post.IsPublish ? "Post published" : "Post unpublished" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during TogglePublish for Post ID: {PostId}", postId);
                return Json(new { success = false, message = "An internal error occurred." });
            }
        }

        [HttpPost]
        [Permission("Dashboard.View")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(nameof(Post.Title), nameof(Post.Description), nameof(Post.MetaDescription), nameof(Post.Body), nameof(Post.Tags), nameof(Post.ShortKey), nameof(Post.IsPublish))] Post post, IFormFile? ImageFile)
        {
            _logger.LogInformation("Attempting to create a new post. Title: '{Title}'", post.Title);

            // Normalize inputs
            post.Title = post.Title?.Trim();
            post.Description = post.Description?.Trim();
            post.MetaDescription = post.MetaDescription?.Trim();
            post.Body = post.Body?.Trim();
            post.Tags = post.Tags?.Trim();
            if (!string.IsNullOrWhiteSpace(post.ShortKey))
            {
                post.ShortKey = post.ShortKey.Trim();
                if (post.ShortKey.Length > 20)
                {
                    post.ShortKey = post.ShortKey[..20];
                    ModelState.Remove("ShortKey");
                }
            }

            // Remove properties we set manually or navigation props
            ModelState.Remove("ImageName");
            ModelState.Remove("User");
            ModelState.Remove("PostComments");
            ModelState.Remove("CreateDate");
            ModelState.Remove("Visit");
            ModelState.Remove("AuthorId");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("Post creation aborted due to invalid ModelState. Errors: {Errors}", errors);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    TempData["ErrorMessage"] = errors;
                }
                return View(post);
            }

            // Handle image upload
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Post creation failed. Invalid image extension '{Extension}' requested.", fileExtension);
                    ModelState.AddModelError("ImageFile", "Only image files are allowed");
                    TempData["ErrorMessage"] = "Only image files are allowed";
                    return View(post);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "posts");
                try
                {
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    post.ImageName = uniqueFileName;
                    _logger.LogInformation("Post cover image saved successfully as: {UniqueName}", uniqueFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write physical image file to disk during post creation.");
                    TempData["ErrorMessage"] = "Server error processing image file.";
                    return View(post);
                }
            }
            else
            {
                post.ImageName = "default.jpg";
                _logger.LogInformation("No image supplied. Using 'default.jpg' for new post cover.");
            }

            post.CreateDate = DateTime.Now;
            post.Visit = 0;
            post.AuthorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

            try
            {
                _context.Posts.Add(post);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved new post to database. Assigned Post ID: {PostId}, Author ID: {AuthorId}", post.PostId, post.AuthorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database save transaction failed for the new post metadata.");
                TempData["ErrorMessage"] = $"Failed to save post: {ex.Message}";
                return View(post);
            }

            TempData["SuccessMessage"] = "Post created successfully";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Permission("Dashboard.View")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind(nameof(Post.PostId), nameof(Post.Title), nameof(Post.Description), nameof(Post.MetaDescription), nameof(Post.Body), nameof(Post.Tags), nameof(Post.ShortKey), nameof(Post.IsPublish))] Post post, IFormFile? ImageFile)
        {
            _logger.LogInformation("Attempting to commit changes for Post ID: {PostId}.", post.PostId);

            // Normalize inputs
            post.Title = post.Title?.Trim();
            post.Description = post.Description?.Trim();
            post.MetaDescription = post.MetaDescription?.Trim();
            post.Body = post.Body?.Trim();
            post.Tags = post.Tags?.Trim();
            if (!string.IsNullOrWhiteSpace(post.ShortKey))
            {
                post.ShortKey = post.ShortKey.Trim();
                if (post.ShortKey.Length > 20)
                {
                    post.ShortKey = post.ShortKey[..20];
                    ModelState.Remove("ShortKey");
                }
            }

            // Remove navigation properties
            ModelState.Remove("User");
            ModelState.Remove("PostComments");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("Post edit aborted due to invalid ModelState for Post ID: {PostId}. Errors: {Errors}", post.PostId, errors);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    TempData["ErrorMessage"] = errors;
                }
                return View(post);
            }

            var existingPost = await _context.Posts.FirstOrDefaultAsync(p => p.PostId == post.PostId);
            if (existingPost == null)
            {
                _logger.LogWarning("Edit failed. Target Post ID {PostId} does not exist in database.", post.PostId);
                TempData["ErrorMessage"] = "Post not found";
                return RedirectToAction(nameof(Index));
            }

            // Handle image upload
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Edit aborted. Invalid image extension '{Extension}' supplied for Post ID: {PostId}.", fileExtension, post.PostId);
                    ModelState.AddModelError("ImageFile", "Only image files are allowed");
                    TempData["ErrorMessage"] = "Only image files are allowed";
                    return View(post);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "posts");
                try
                {
                    Directory.CreateDirectory(uploadsFolder);

                    // Delete old image if not default
                    if (!string.IsNullOrEmpty(existingPost.ImageName) && existingPost.ImageName != "default.jpg")
                    {
                        var oldImagePath = Path.Combine(uploadsFolder, existingPost.ImageName);
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                            _logger.LogInformation("Deleted old cover image file: {OldImage} for Post ID: {PostId}", existingPost.ImageName, post.PostId);
                        }
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    existingPost.ImageName = uniqueFileName;
                    _logger.LogInformation("Successfully replaced physical cover image with: {UniqueName} for Post ID: {PostId}", uniqueFileName, post.PostId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred handling file operations during image replacement for Post ID: {PostId}", post.PostId);
                    TempData["ErrorMessage"] = "Server error updating image.";
                    return View(post);
                }
            }

            existingPost.Title = post.Title;
            existingPost.Description = post.Description;
            existingPost.MetaDescription = post.MetaDescription;
            existingPost.Body = post.Body;
            existingPost.Tags = post.Tags;
            existingPost.ShortKey = post.ShortKey;
            existingPost.IsPublish = post.IsPublish;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully committed post updates to database for Post ID: {PostId}", post.PostId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database save failed while modifying Post ID: {PostId}", post.PostId);
                TempData["ErrorMessage"] = $"Failed to update post: {ex.Message}";
                return View(post);
            }

            TempData["SuccessMessage"] = "Post updated successfully";
            return RedirectToAction(nameof(Index));
        }
    }
}