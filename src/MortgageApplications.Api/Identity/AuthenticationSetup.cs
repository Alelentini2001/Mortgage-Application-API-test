using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MortgageApplications.Api.Identity;

/// <summary>
/// JWT bearer authentication and the secure-by-default authorization policy (Phase 7, ADR 0004). Learn,
/// "Configure JWT bearer authentication": signature, issuer, audience and lifetime are validated, and
/// <c>MapInboundClaims = false</c> keeps <c>sub</c> as <c>sub</c>.
/// </summary>
internal static partial class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<DevAuthOptions>().BindConfiguration(DevAuthOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DevAuthOptions>, DevAuthOptionsValidator>();
        services.AddSingleton<TokenKeys>();
        services.AddSingleton<DevTokenIssuer>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TokenKeys>(Configure);

        // Every endpoint needs an authenticated caller unless it is marked AllowAnonymous (Learn, "Secure data":
        // the fallback policy "is the preferred way to require all users to be authenticated").
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        return services;
    }

    private static void Configure(JwtBearerOptions options, IOptions<JwtOptions> jwt, TokenKeys keys)
    {
        // Trust comes only from the Jwt:* settings. The framework also binds Authentication:Schemes:Bearer:*, where an
        // Authority or MetadataAddress would make the handler fetch and trust that server's keys (security gate).
        options.Authority = null;
        options.MetadataAddress = null!; // the property is not nullable, but the handler treats null as "not set"
        options.MapInboundClaims = false;
        // A5: no error_description, so no validation exception text reaches the client.
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys.ValidationKeys,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = SubjectClaim.Type,
        };
        options.Events = new JwtBearerEvents
        {
            // A missing, blank, repeated or over-long sub is a 401 here, never a 500 later (A4).
            OnTokenValidated = context =>
            {
                if (!SubjectClaim.IsValid(context.Principal, context.SecurityToken))
                {
                    TokenRejected(context.HttpContext, "InvalidSubject");
                    context.Fail("The token has no valid subject.");
                }

                return Task.CompletedTask;
            },
            // Security signal: the framework's own rejection logs are hidden at Warning (they carry token values, AU92).
            // This event says only that a token was rejected and the failure's exception type name.
            OnAuthenticationFailed = context =>
            {
                TokenRejected(context.HttpContext, context.Exception.GetType().Name);
                return Task.CompletedTask;
            },
            // With IncludeErrorDetails off the handler sends a bare "Bearer". A rejected token still gets
            // error="invalid_token" (RFC 6750), without a description; a request with no token gets neither.
            OnChallenge = context =>
            {
                if (context.AuthenticateFailure is not null)
                {
                    context.Error = "invalid_token";
                }

                return Task.CompletedTask;
            },
        };
    }

    private static void TokenRejected(HttpContext httpContext, string failureType)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthenticationSetup));
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogTokenRejected(logger, failureType);
        }
    }

    [LoggerMessage(EventId = 8, EventName = "TokenRejected", Level = LogLevel.Information,
        Message = "Bearer token rejected: {FailureType}.")]
    private static partial void LogTokenRejected(ILogger logger, string failureType);
}
