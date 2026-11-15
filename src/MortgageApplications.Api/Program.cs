using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MortgageApplications.Api.Applications;
using MortgageApplications.Api.Assessments;
using MortgageApplications.Api.Configuration;
using MortgageApplications.Api.Errors;
using MortgageApplications.Api.Identity;
using MortgageApplications.Api.Observability;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Affordability rules (rules section 1): bound from configuration and validated at startup, so the app never
// runs with invalid limits (security S4). The calculator copies the values once.
builder.Services.AddOptions<AffordabilityOptions>().BindConfiguration("Affordability").ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AffordabilityOptions>, AffordabilityOptionsValidator>();
builder.Services.AddSingleton(sp => new AffordabilityCalculator(sp.GetRequiredService<IOptions<AffordabilityOptions>>().Value));

// Background assessments (Phase 6, ADR 0003): one bounded in-process queue and one worker. The queue waits when
// full instead of dropping (P6-D2); its capacity is validated at startup.
builder.Services.AddOptions<AssessmentQueueOptions>()
    .BindConfiguration(AssessmentQueueOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ChannelAssessmentQueue>();
builder.Services.AddSingleton<IAssessmentQueue>(sp => sp.GetRequiredService<ChannelAssessmentQueue>());
builder.Services.AddHostedService<AssessmentWorker>();

// Telemetry (Phase 5, P5-D1 A): ASP.NET Core server spans and meters, our worker's span and metrics
// (AssessmentTelemetry), and logs, through the OpenTelemetry SDK. The OTLP exporter is added only when an OTLP
// endpoint is configured (configuration includes environment variables), so tests, CI and a plain `dotnet run` never
// try to connect. The ASP.NET Core instrumentation redacts query-string values.
builder.Services.AddSingleton<AssessmentTelemetry>();
var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        // AddService would override a service name set through OTEL_SERVICE_NAME or as service.name in
        // OTEL_RESOURCE_ATTRIBUTES, so the default name applies only when neither sets one.
        var namedByAttributes = builder.Configuration["OTEL_RESOURCE_ATTRIBUTES"]?
            .Contains("service.name=", StringComparison.Ordinal) ?? false;
        if (string.IsNullOrWhiteSpace(builder.Configuration["OTEL_SERVICE_NAME"]) && !namedByAttributes)
        {
            resource.AddService("mortgage-applications");
        }
    })
    .WithTracing(tracing => tracing
        // user_agent.original is client-controlled text and not in the rules section 6 allowlist: drop it. The
        // package has no option for it; EnrichWithHttpRequest runs after the instrumentation sets its tags.
        .AddAspNetCoreInstrumentation(options =>
            options.EnrichWithHttpRequest = (activity, _) => activity.SetTag("user_agent.original", null))
        .AddSource(AssessmentTelemetry.Name))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter(AssessmentTelemetry.Name)
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("Microsoft.AspNetCore.Server.Kestrel"))
    .WithLogging();

// The base endpoint or any signal-specific one turns the exporter on. With only a signal-specific key, the other
// signals use the exporter's default endpoint (localhost), where nothing listens in a container.
string[] otlpEndpointKeys =
[
    "OTEL_EXPORTER_OTLP_ENDPOINT",
    "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
    "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
    "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
];
if (otlpEndpointKeys.Any(key => !string.IsNullOrWhiteSpace(builder.Configuration[key])))
{
    openTelemetry.UseOtlpExporter();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICurrentUser, DevelopmentCurrentUser>();
builder.Services.AddScoped<MortgageApplicationService>();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("MortgageApplications")
    ?? throw new InvalidOperationException("Connection string 'MortgageApplications' is missing."));

// Strict JSON (P3-D8): unknown properties, numbers sent as strings and duplicate properties are 400s;
// enums are names, not numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.AllowDuplicateProperties = false;
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
    // Development only: production runs migrations as a separate step (ADR 0002).
    await app.Services.MigrateDatabaseAsync();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/health", () => TypedResults.Ok("Healthy"));
app.MapApplicationEndpoints();

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program { }
