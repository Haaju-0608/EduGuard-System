using ParentalShieldBE.Middlewares;

namespace ParentalShieldBE.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Thêm Global Exception Middleware vào pipeline (nên đặt đầu tiên).
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
    /// Request Logging Middleware

    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
