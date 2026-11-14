var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Endpoints are added in Phase 3. This minimal health probe keeps the skeleton runnable.
app.MapGet("/health", () => TypedResults.Ok("Healthy"));

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
