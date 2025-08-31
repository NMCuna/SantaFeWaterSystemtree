using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using System.Linq;
using System.Security.Claims;

namespace SantaFeWaterSystem.Filters
{
    public class RequirePrivacyAgreementAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var httpContext = context.HttpContext;
            var db = (ApplicationDbContext)httpContext.RequestServices
                .GetService(typeof(ApplicationDbContext));

            // ✅ Skip privacy pages (prevent redirect loop)
            var path = httpContext.Request.Path.Value?.ToLower() ?? "";
            if (path.Contains("/privacy/agree") || path.Contains("/privacy/agreepolicy"))
            {
                base.OnActionExecuting(context);
                return;
            }

            // ✅ Get userId from claims instead of session
            var userIdClaim = httpContext.User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                base.OnActionExecuting(context);
                return;
            }

            // ✅ Get user + consumer
            var user = db.Users
                .Include(u => u.Consumer)
                .FirstOrDefault(u => u.Id == userId);

            if (user == null || user.Consumer == null)
            {
                base.OnActionExecuting(context);
                return; // admin/staff, skip check
            }

            // ✅ Get latest published policy
            var latestPolicy = db.PrivacyPolicies
                .OrderByDescending(p => p.Version)
                .FirstOrDefault();

            if (latestPolicy == null)
            {
                base.OnActionExecuting(context);
                return;
            }

            // ✅ Get latest agreement
            var agreement = db.UserPrivacyAgreements
                .AsNoTracking()
                .Where(a => a.ConsumerId == user.Consumer.Id)
                .OrderByDescending(a => a.PolicyVersion)
                .FirstOrDefault();

            // ✅ Redirect if missing or outdated
            if (agreement == null || agreement.PolicyVersion < latestPolicy.Version)
            {
                context.Result = new RedirectToActionResult(
                    "Agree",
                    "Privacy",
                    new { version = latestPolicy.Version }
                );
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
