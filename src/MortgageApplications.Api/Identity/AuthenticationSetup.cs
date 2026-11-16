using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MortgageApplications.Api.Identity;

/// <summary>
/// JWT bearer authentication and the secure-by-default authorization policy (Phase 7, ADR 0004). Learn,
/// "Configure JWT bearer authentication": signature, issuer, audience and lifetime are validated, and
/// <c>MapInboundClaims = false</c> keeps <c>sub</c> as <c>sub</c>.
/// </summary>
internal static class AuthenticationSetup
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
                if (!SubjectClaim.IsValid(context.Principal))
                {
                    context.Fail("The token has no valid subject.");
                }

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
}
