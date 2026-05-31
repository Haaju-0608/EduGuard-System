using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;

namespace EduGuardProject.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class SupabaseAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly AppRole[] _allowedRoles;

        // Cho phép truyền vào nhiều Role (VD: [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)])
        public SupabaseAuthorizeAttribute(params AppRole[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // 1. Lấy Token từ Header
            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.OnFail("Missing or invalid token format."));
                return;
            }

            var tokenString = authHeader.Substring("Bearer ".Length).Trim();
            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(tokenString))
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.OnFail("Invalid token."));
                return;
            }

            // 2. Đọc Claim "sub" (UserId)
            var jwtToken = handler.ReadJwtToken(tokenString);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.OnFail("User ID not found in token."));
                return;
            }

            // 3. Nếu API có yêu cầu Role cụ thể thì mới check DB
            if (_allowedRoles != null && _allowedRoles.Length > 0)
            {
                // Gọi IUserService thông qua Dependency Injection của HttpContext
                var userService = context.HttpContext.RequestServices.GetService<IUserService>();
                if (userService == null)
                {
                    context.Result = new StatusCodeResult(500); // Lỗi server chưa đăng ký Service
                    return;
                }

                var userProfile = await userService.GetUserByIdAsync(userId);

                if (userProfile == null)
                {
                    context.Result = new UnauthorizedObjectResult(ApiResponse<object>.OnFail("User does not exist."));
                    return;
                }

                // 4. Kiểm tra quyền
                if (!_allowedRoles.Contains(userProfile.Role))
                {
                    context.Result = new ObjectResult(ApiResponse<object>.OnFail("Forbidden: You do not have permission to access this resource."))
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    return;
                }
            }

            // Lưu UserId vào HttpContext để các Controller khác có thể xài nếu cần (ví dụ: lấy ID người đang đăng nhập)
            context.HttpContext.Items["UserId"] = userId;
        }
    }
}
