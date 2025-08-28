using System;
using System.Threading.Tasks;
using AstaLegheFC.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AstaLegheFC.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ActiveLicenseAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;
            if (!(http.User?.Identity?.IsAuthenticated ?? false))
            {
                // Lascio che [Authorize] gestisca il redirect al login
                return;
            }

            var db = http.RequestServices.GetService(typeof(AppDbContext)) as AppDbContext;
            var uid = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (db == null || string.IsNullOrEmpty(uid))
            {
                context.Result = new RedirectToActionResult("Index", "Site", null);
                return;
            }

            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);

            // Regola richiesta:
            // LicensePlan NULL  -> BLOCCO
            // LicensePlan 'LIFE' -> OK sempre
            // Altri piani (1M/6M/12M) -> OK se LicenseExpiresAt > now, altrimenti BLOCCO
            var plan = user?.LicensePlan?.Trim();
            var nowUtc = DateTime.UtcNow;

            var attivo =
                !string.IsNullOrEmpty(plan) && (
                    plan.Equals("lifetime", StringComparison.OrdinalIgnoreCase) ||
                    (user?.LicenseExpiresAt != null && user.LicenseExpiresAt.Value.UtcDateTime > nowUtc)
                );

            if (!attivo)
            {
                var returnUrl = http.Request.Path + http.Request.QueryString;
                context.Result = new RedirectToActionResult("Index", "Site", new { needPlan = "1", returnUrl });
            }
        }
    }
}
