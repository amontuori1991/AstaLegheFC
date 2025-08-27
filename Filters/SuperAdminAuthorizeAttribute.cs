using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AstaLegheFC.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SuperAdminAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public const string SessionKey = "IsSuperAdmin";

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var isSuper = context.HttpContext.Session?.GetString(SessionKey);
            if (!string.Equals(isSuper, "true", StringComparison.Ordinal))
            {
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "SuperAdmin", new { returnUrl });
            }
            return Task.CompletedTask;
        }
    }
}
