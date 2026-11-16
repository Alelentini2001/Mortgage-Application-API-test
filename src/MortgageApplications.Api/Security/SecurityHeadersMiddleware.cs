namespace MortgageApplications.Api.Security;

/// <summary>
/// Baseline HTTP security and anti-caching headers on every response (P8-D3 A, A3): stops MIME sniffing, framing,
/// referrer leakage and intermediary caching of financial data or bearer tokens.
/// </summary>
internal static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            return next(context);
        });
}
