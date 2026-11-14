using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MortgageApplications.Api.Applications;
using MortgageApplications.Api.Configuration;
using MortgageApplications.Api.Errors;
using MortgageApplications.Api.Identity;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Affordability rules (rules section 1): bound from configuration and validated at startup, so the app never
// runs with invalid limits (security S4). The calculator copies the values once.
builder.Services.AddOptions<AffordabilityOptions>().BindConfiguration("Affordability").ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AffordabilityOptions>, AffordabilityOptionsValidator>();
builder.Services.AddSingleton(sp => new AffordabilityCalculator(sp.GetRequiredService<IOptions<AffordabilityOptions>>().Value));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICurrentUser, DevelopmentCurrentUser>();
builder.Services.AddScoped<MortgageApplicationService>();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("MortgageApplications")
    ?? throw new InvalidOperationException("Connection string 'MortgageApplications' is missing."));

// Strict JSON (P3-D8): unknown properties and numbers sent as strings are 400s; enums are names, not numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddValidation();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ValidationErrorKeys.UseJsonNames);
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

// .NET 10 suppresses logs and metrics for exceptions a handler reports as handled. Keep them for real failures;
// only client errors (malformed body, wrong content type) are suppressed.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = context => context.Exception is BadHttpRequestException,
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Development only: production runs migrations as a separate step (ADR in Phase 4).
    await app.Services.MigrateDatabaseAsync();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/health", () => TypedResults.Ok("Healthy"));
app.MapApplicationEndpoints();

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program { }
