using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using MortgageApplications.Api.Identity;

namespace MortgageApplications.Api.Security;

/// <summary>
/// Built-in fixed-window rate limiting on write endpoints (P8-D1 A, A1): partitioned by the authenticated caller's
/// <c>sub</c> claim on <c>/api/*</c> and a <c>"dev-token"</c> partition on <c>POST /dev/token</c>. Excess requests
/// fail immediately (<c>QueueLimit = 0</c>) with 429 <c>ProblemDetails</c> (via <c>UseStatusCodePages</c>) and a
/// <c>Retry-After</c> header in whole seconds.
/// </summary>
internal static class RateLimitingSetup
{
    public const string WritePolicyName = "write";

    public static IServiceCollection AddWriteRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var windowSeconds = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value.WindowSeconds;
                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Clamp((int)Math.Ceiling(retryAfter.TotalSeconds), 1, windowSeconds)
                    : windowSeconds;
                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            options.AddPolicy(WritePolicyName, httpContext =>
            {
                var rateOptions = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var partitionKey = httpContext.User.FindFirst(SubjectClaim.Type)?.Value ?? "dev-token";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateOptions.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });
        });

        return services;
    }
}
