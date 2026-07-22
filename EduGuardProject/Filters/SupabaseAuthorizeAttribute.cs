using System.Security.Claims;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EduGuardProject.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class SupabaseAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly AppRole[] _allowedRoles;

    public SupabaseAuthorizeAttribute(params AppRole[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(
                ApiResponse<object>.OnFail("Missing, expired, or invalid access token."));
            return;
        }

        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(
                ApiResponse<object>.OnFail("User ID not found in token."));
            return;
        }

        var currentUserService = context.HttpContext.RequestServices.GetService<ICurrentUserService>();
        if (currentUserService == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var userProfile = await currentUserService.GetCurrentUserAsync();
        if (userProfile == null || userProfile.Id != userId)
        {
            context.Result = new UnauthorizedObjectResult(
                ApiResponse<object>.OnFail("User profile does not exist or is inactive."));
            return;
        }

        if (_allowedRoles.Length > 0 && !userProfile.Role.IsInRoles(_allowedRoles))
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.OnFail("Forbidden: You do not have permission to access this resource."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        context.HttpContext.Items["UserId"] = userId;

        context.HttpContext.Items["Role"] = userProfile.Role.ToCanonical();       
        context.HttpContext.Items["InstitutionId"] = userProfile.InstitutionId;
    }
}
