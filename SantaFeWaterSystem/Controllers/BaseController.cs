using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims; 


namespace SantaFeWaterSystem.Controllers
{
    public class BaseController : Controller
    {
        protected readonly PermissionService _permissionService;
        protected readonly ApplicationDbContext _context;
        protected readonly AuditLogService _audit;


        public BaseController(PermissionService permissionService, ApplicationDbContext context, AuditLogService audit)
        {
            _permissionService = permissionService;
            _context = context;
            _audit = audit;

        }


        // ================== BASE CONTROLER EXECUTION ==================

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        int? userId = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId))
            {
                userId = parsedId;
            }
        }

        if (userId != null)
        {
            // Load permissions
            var permissions = await _permissionService.GetUserPermissionsAsync(userId.Value);
            ViewBag.UserPermissions = permissions;

            // Load 2FA flag from database
            var user = await _context.Users.FindAsync(userId.Value);
            ViewBag.IsMfaEnabled = user?.IsMfaEnabled ?? false;
        }
        else
        {
            ViewBag.UserPermissions = new List<string>();
            ViewBag.IsMfaEnabled = false;
        }

        await next();
    }
}
}