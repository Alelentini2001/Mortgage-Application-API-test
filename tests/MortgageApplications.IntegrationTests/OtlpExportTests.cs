using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 5 export: the OTLP exporter is added only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (read from
/// configuration, Q8). The tests use OTLP/HTTP protobuf (<c>OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf</c>), so a
/// plain loopback <see cref="HttpListener"/> can stand in for the collector; disposing the host flushes every signal.
/// </summary>
[Collection(TelemetryGroup.Name)]
public sealed class OtlpExportTests
{
    // O24: without an endpoint nothing is exported. With the protocol set to http/protobuf, the exporter's default
    // endpoint would be localhost:4318, so the test listens there (IPv4 and IPv6 loopback) and sees no connection.
    [Fact]
    public async Task Without_an_endpoint_no_otlp_traffic_is_sent()
    {
        using var ipv4 = TryListen(IPAddress.Loopback, 4318);
        using var ipv6 = TryListen(IPAddress.IPv6Loopback, 4318);
        if (ipv4 is null)
        {
            Assert.Skip("Port 4318 on 127.0.0.1 is in use, so the default OTLP/HTTP endpoint cannot be watched.");
        }

        await using var factory = new ApiFactory();
        var host = ExportingHost(factory, endpoint: null);

        await Telemetry.AssessAsync(host.CreateClient(), Http.BaseBody);
        await host.DisposeAsync();

        Assert.False(ipv4.Pending());
        Assert.False(ipv6?.Pending() ?? false);
    }

    // O25: with an endpoint, traces, metrics and logs are all exported to it (positive control for O24).
    [Fact]
    public async Task With_an_endpoint_traces_metrics_and_logs_are_exported()
    {
        using var collector = new FakeOtlpCollector();
        await using var factory = new ApiFactory();
        var host = ExportingHost(factory, collector.Endpoint);

        await Telemetry.AssessAsync(host.CreateClient(), Http.BaseBody);
        await host.DisposeAsync();

        var paths = collector.Requests.Select(r => r.Path).Distinct().Order(StringComparer.Ordinal);
        Assert.Equal(["/v1/logs", "/v1/metrics", "/v1/traces"], paths);
    }

    // O26 (nice): the exported bytes hold our span, our instruments and the service name (so AddSource, AddMeter and
    // the resource are registered), and none of E1's numbers or Summary text.
    [Fact]
    public async Task Exported_data_has_our_span_and_instruments_and_no_assessment_numbers()
    {
        using var collector = new FakeOtlpCollector();
        await using var factory = new ApiFactory();
        var host = ExportingHost(factory, collector.Endpoint);

        await Telemetry.AssessAsync(host.CreateClient(), Http.BaseBody);
        await host.DisposeAsync();

        var exported = string.Concat(collector.Requests.Select(r => Encoding.UTF8.GetString(r.Body)));
        Assert.Contains("assessment.process", exported, StringComparison.Ordinal);
        Assert.Contains(Telemetry.Counter, exported, StringComparison.Ordinal);
        Assert.Contains(Telemetry.Histogram, exported, StringComparison.Ordinal);
        Assert.Contains("mortgage-applications", exported, StringComparison.Ordinal);
        Assert.Empty(GoldenExample.Get("E1").Forbidden.Find(exported));
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ExportingHost(ApiFactory factory, string? endpoint)
    {
        var settings = new Dictionary<string, string?> { ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf" };
        if (endpoint is not null)
        {
            settings["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
        }

        return factory.WithWebHostBuilder(
            builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings)),
            runWorker: true);
    }

    private static TcpListener? TryListen(IPAddress address, int port)
    {
        var listener = new TcpListener(address, port);
        try
        {
            listener.Start();
            return listener;
        }
        catch (SocketException)
        {
            listener.Dispose();
            return null;
        }
    }

    /// <summary>An OTLP/HTTP stand-in on a free loopback port: keeps every request's path and body, answers 200.</summary>
    private sealed class FakeOtlpCollector : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly ConcurrentQueue<(string Path, byte[] Body)> _requests = new();
        private readonly Task _loop;

        public FakeOtlpCollector()
        {
            Endpoint = $"http://127.0.0.1:{FreePort()}";
            _listener.Prefixes.Add($"{Endpoint}/");
            _listener.Start();
            _loop = Task.Run(ServeAsync);
        }

        public string Endpoint { get; }

        public IReadOnlyList<(string Path, byte[] Body)> Requests => [.. _requests];

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
            _loop.Wait(TimeSpan.FromSeconds(5));
        }

        private static int FreePort()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }

                using var body = new MemoryStream();
                await context.Request.InputStream.CopyToAsync(body);
                _requests.Enqueue((context.Request.Url!.AbsolutePath, body.ToArray()));
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.Close();
            }
        }
    }
}
