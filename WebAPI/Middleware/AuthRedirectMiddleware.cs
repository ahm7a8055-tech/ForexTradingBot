using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Middleware
{
    public class AuthRedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string[] _protectedPaths = { "/", "/index.html", "/config.html" };
        private const string LoginPagePath = "/login.html";

        public AuthRedirectMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestPath = context.Request.Path.Value;

            // Check if the request path is one of the protected static HTML files
            if (_protectedPaths.Contains(requestPath, StringComparer.OrdinalIgnoreCase))
            {
                // If the user is not authenticated, redirect to the login page
                if (context.User.Identity == null || !context.User.Identity.IsAuthenticated)
                {
                    context.Response.Redirect(LoginPagePath);
                    return; // Short-circuit the pipeline
                }
            }

            await _next(context);
        }
    }
}
