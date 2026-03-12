using TaskManagement.Data;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Models;
namespace TaskManagement
{
    public class ApiTokenMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiTokenMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, AccountDbContext db)
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/swagger") ||
                path.StartsWithSegments("/api/Auth/login") ||
                path.StartsWithSegments("/api/Auth/ForgotPassword") || 
                path.StartsWithSegments("/api/Auth/VerifyOtp") ||      
                path.StartsWithSegments("/api/Auth/ResetPassword"))   

            {
                await _next(context);
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing token.");
                return;
            }

            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(7).Trim()
                : authHeader.Trim();

            var apiToken = await db.ApiTokens
                .SingleOrDefaultAsync(t => t.Token == token && !t.Revoked && t.ExpiresAt > DateTime.UtcNow);

            if (apiToken == null)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid or expired token.");
                return;
            }

            await _next(context);
        }
    }
}
