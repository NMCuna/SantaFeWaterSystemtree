using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Filters;



[Authorize(Roles = "Admin,Staff,User")]
[RequirePrivacyAgreement]
public class AnnouncementController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public AnnouncementController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }


    // ================== GetCurrentUserId ==================

    // Helper: get numeric user ID from claims
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (int.TryParse(userIdClaim, out int userId))
            return userId;
        return null;
    }


   

    // ================== COMMUNITY FEED USER,ADMIN,STAFF ==================

    // Display all posts with feedback
    [Authorize(Roles = "Admin,Staff,User")]
    public async Task<IActionResult> Index()
    {
        var posts = await _context.Announcements
            .Where(a => a.IsActive == true) 
            .Include(a => a.Admin)
            .Include(a => a.Feedbacks!)
                .ThenInclude(f => f.User)
            .Include(a => a.Feedbacks!)
                .ThenInclude(f => f.FeedbackLikes)
            .Include(a => a.Feedbacks!)
                .ThenInclude(f => f.Comments!)
                    .ThenInclude(c => c.User)
            .OrderByDescending(a => a.PostedAt)
            .ToListAsync();

        return View(posts);
    }





    
    // ================== CREATE ANNOUNCEMENT ADMIN,STAFF=================

    // Admin create post (GET)
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public IActionResult Create() => View();

    // Admin create post (POST)
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    public async Task<IActionResult> Create(string? title, string? content, IFormFile? image)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Forbid();

        string? imagePath = null;
        if (image != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream);

            imagePath = "/uploads/" + fileName;
        }

        var post = new Announcement
        {
            Title = title,
            Content = content,
            ImagePath = imagePath,
            AdminId = userId.Value,
            PostedAt = DateTime.UtcNow
        };

        _context.Announcements.Add(post);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }




    // ================== ADD FEEDBACK USER,ADMIN,STAFF ==================


    // User adds feedback (text/image)
    [HttpPost]
    [Authorize(Roles = "Admin,Staff,User")]
    public async Task<IActionResult> AddFeedback(int announcementId, string? comment, IFormFile? image)
    {
        if (string.IsNullOrEmpty(comment) && image == null)
            return BadRequest("Please provide a comment or an image.");

        var userId = GetCurrentUserId();
        if (userId == null) return Forbid();

        string? imagePath = null;
        if (image != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream);

            imagePath = "/uploads/" + fileName;
        }

        var feedback = new Feedback
        {
            AnnouncementId = announcementId,
            UserId = userId.Value,
            Comment = comment,
            ImagePath = imagePath,
            SubmittedAt = DateTime.UtcNow
        };

        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        await _context.Entry(feedback).Reference(f => f.User).LoadAsync();
        await _context.Entry(feedback).Reference(f => f.Announcement).LoadAsync();

        return PartialView("_FeedbackItem", feedback);
    }




    // ================== EDIT FEEDBACK USER,ADMIN,STAFF ==================


    // Edit feedback
    [Authorize(Roles = "Admin,Staff,User")]
    [HttpGet]
    public async Task<IActionResult> EditFeedback(int id)
    {
        var feedback = await _context.Feedbacks.Include(f => f.User).FirstOrDefaultAsync(f => f.Id == id);
        if (feedback == null) return NotFound();

        var userId = GetCurrentUserId();
        if (userId == null || (feedback.UserId != userId && !User.IsInRole("Admin"))) return Forbid();

        return View(feedback);
    }

    [Authorize(Roles = "Admin,Staff,User")]
    [HttpPost]
    public async Task<IActionResult> EditFeedback(int id, string? comment, IFormFile? image)
    {
        var feedback = await _context.Feedbacks.FirstOrDefaultAsync(f => f.Id == id);
        if (feedback == null) return NotFound();

        var userId = GetCurrentUserId();
        if (userId == null || (feedback.UserId != userId && !User.IsInRole("Admin"))) return Forbid();

        feedback.Comment = comment;

        if (image != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream);

            feedback.ImagePath = "/uploads/" + fileName;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }







   
    // ================== ADD FEEDBACK COMMENT USER,ADMIN,STAFF ==================

    // Add comment to feedback
    [Authorize(Roles = "Admin,Staff,User")]
    [HttpPost]
    public async Task<IActionResult> AddFeedbackComment(int feedbackId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Comment cannot be empty.";
            return RedirectToAction("Index");
        }

        var userId = GetCurrentUserId();
        if (userId == null) return Forbid();

        var comment = new FeedbackComment
        {
            FeedbackId = feedbackId,
            Content = content,
            UserId = userId.Value,
            CommentedAt = DateTime.UtcNow
        };

        _context.FeedbackComments.Add(comment);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }




    // ================== DELETE COMMENT ==================
    [Authorize(Roles = "Admin,Staff,User")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id)   // ❌ removed [FromBody]
    {
        var comment = await _context.FeedbackComments
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment == null)
            return Json(new { success = false, message = "Comment not found." });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Json(new { success = false, message = "Unauthorized request." });

        // Admin, Staff OR Owner can delete
        if (User.IsInRole("Admin") || User.IsInRole("Staff") || comment.UserId == userId.Value)
        {
            _context.FeedbackComments.Remove(comment);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Comment deleted successfully." });
        }

        return Json(new { success = false, message = "You are not allowed to delete this comment." });
    }


    // ================== DELETE FEEDBACK USER,ADMIN,STAFF ==================
    [Authorize(Roles = "Admin,Staff,User")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFeedback(int id)   // ❌ removed [FromBody]
    {
        var feedback = await _context.Feedbacks
            .Include(f => f.Comments) // Ensure comments are loaded
            .FirstOrDefaultAsync(f => f.Id == id);

        if (feedback == null)
            return Json(new { success = false, message = "Feedback not found." });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Json(new { success = false, message = "Unauthorized request." });

        // Admin, Staff OR Owner can delete
        if (User.IsInRole("Admin") || User.IsInRole("Staff") || feedback.UserId == userId.Value)
        {
            // Remove comments first (cascade delete)
            if (feedback.Comments != null && feedback.Comments.Any())
            {
                _context.FeedbackComments.RemoveRange(feedback.Comments);
            }

            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Feedback deleted successfully." });
        }

        return Json(new { success = false, message = "You are not allowed to delete this feedback." });
    }



    // ================== TOGGLE FEEDBACK LIKE USER,ADMIN,STAFF ==================


    // Like/unlike feedback
    [Authorize(Roles = "Admin,Staff,User")]
    [HttpPost]
    public async Task<IActionResult> ToggleFeedbackLike(int feedbackId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Forbid();

        var existingLike = await _context.FeedbackLikes
            .FirstOrDefaultAsync(l => l.FeedbackId == feedbackId && l.UserId == userId.Value);

        if (existingLike != null)
            _context.FeedbackLikes.Remove(existingLike); // unlike
        else
            _context.FeedbackLikes.Add(new FeedbackLike
            {
                FeedbackId = feedbackId,
                UserId = userId.Value,
                LikedAt = DateTime.UtcNow
            });

        await _context.SaveChangesAsync();

        // Get updated like count
        var likeCount = await _context.FeedbackLikes
            .CountAsync(l => l.FeedbackId == feedbackId);

        return Json(new { likeCount });
    }




    // ================== EDIT ANNOUNCEMENT ==================


    // Admin edit/delete announcement
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> EditAnnouncement(int id)
    {
        var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (announcement == null) return NotFound();
        return View(announcement);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    public async Task<IActionResult> EditAnnouncement(int id, string? title, string? content, IFormFile? image)
    {
        var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (announcement == null) return NotFound();

        announcement.Title = title;
        announcement.Content = content;

        if (image != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream);

            announcement.ImagePath = "/uploads/" + fileName;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        var announcement = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (announcement == null) return NotFound();

        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
