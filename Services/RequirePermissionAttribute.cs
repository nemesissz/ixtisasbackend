using Microsoft.AspNetCore.Mvc.Filters;

namespace MmuIspApi.Services;

// permissions.ts:hasPerm-in server tərəfi qarşılığı: superadmin həmişə keçir,
// digərləri üçün JWT-dəki "perm" claim-lərində kod olmalıdır.
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _code;
    public RequirePermissionAttribute(string code) => _code = code;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }
        if (user.IsInRole("superadmin")) return;
        if (user.Claims.Any(c => c.Type == "perm" && c.Value == _code)) return;
        context.Result = new Microsoft.AspNetCore.Mvc.ForbidResult();
    }
}
