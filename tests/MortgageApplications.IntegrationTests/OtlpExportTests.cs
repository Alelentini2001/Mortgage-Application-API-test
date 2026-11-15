using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 5 export: the OTLP exporter is added only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (read from
/// configuration, Q8). The tests use OTLP/HTTP protobuf (<c>OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf</c>), so a
/// plain loopback <see cref="HttpListener"/> can stand in for the collector. The export intervals are set to 100 ms and
/// each test waits (bounded) for the data while the host is still running, then disposes it: relying on the flush at
/// shutdown alone failed on the Linux CI runner, where the three positive tests received nothing.
/// <para>
/// The settings go in with <c>UseSetting</c> (per host, never an environment variable): <c>Program</c> reads them
/// before <c>Build()</c>, and <c>WebApplicationFactory</c> applies <c>ConfigureAppConfiguration</c> sources only at
/// <c>Build()</c>, so an <c>AddInMemoryCollection</c> value is not visible there yet.
/// </para>
/// </summary>
[Collection(TelemetryGroup.Name)]
public sealed class OtlpExportTests
{
    // O24: without an endpoint nothing is exported. With the protocol set to http/protobuf and no endpoint key, an
    // exporter registered unconditionally would send to its default endpoint, localhost:4318, so the test listens
    // there (IPv4 and IPv6 loopback) and expects no connection. The port is fixed on purpose: any endpoint key that
    // points the exporter at a free port also turns the exporter on (Program checks all four keys), so only the
    // default port can catch an unconditional registration. When 4318 is taken the test is skipped, visibly, with
    // the reason; it never passes silently.
    [Fact]
    public async Task Without_an_endpoint_no_otlp_traffic_is_sent()
    {
        using var ipv4 = TryListen(IPAddress.Loopback, 4318);
        using var ipv6 = TryListen(IPAddress.IPv6Loopback, 4318);
        if (ipv4 is null)
        {
            Assert.Skip("port 4318 in use");
        }

        await using var factory = new ApiFactory();
        var host = ExportingHost(factory);

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
        var host = ExportingHost(factory, ("OTEL_EXPORTER_OTLP_ENDPOINT", collector.Endpoint));

        await Telemetry.AssessAsync(host.CreateClient(), Http.BaseBody);
        await collector.WaitForAsync(r => r.Select(x => x.Path).Distinct().Count() == 3, "logs, metrics and traces");
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
        var host = ExportingHost(factory, ("OTEL_EXPORTER_OTLP_ENDPOINT", collector.Endpoint));

        await Telemetry.AssessAsync(host.CreateClient(), Http.BaseBody);
        await collector.WaitForAsync(
            r => Text(r).Contains("assessment.process", StringComparison.Ordinal)
                && Text(r).Contains(Telemetry.Counter, StringComparison.Ordinal)
                && Text(r).Contains(Telemetry.Histogram, StringComparison.Ordinal),
            "the worker span and both instruments");
        await host.DisposeAsync();

        var exported = Text(collector.Requests);
        Assert.Contains("assessment.process", exported, StringComparison.Ordinal);
        Assert.Contains(Telemetry.Counter, exported, StringComparison.Ordinal);
        Assert.Contains(Telemetry.Histogram, exported, StringComparison.Ordinal);
        Assert.Contains("mortgage-applications", exported, StringComparison.Ordinal);
        Assert.Empty(GoldenExample.Get("E1").Forbidden.Find(exported));
    }

    // O25b (review gate): a signal-specific endpoint alone turns the exporter on; with only the traces key set,
    // trace data reaches the collector at that URL.
    [Fact]
    public async Task Traces_endpoint_alone_exports_traces()
    {
        using var collector = new FakeOtlpCollector();
        await using var factory = new ApiFactory();
        var host = ExportingHost(factory, ("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", $"{collector.Endpoint}/v1/traces"));

        await Telemetry.AssessAsync(host.CreateClient(), Http.BaseBody);
        await collector.WaitForAsync(
            r => Text(r.Where(x => x.Path == "/v1/traces")).Contains("assessment.process", StringComparison.Ordinal),
            "the worker span on /v1/traces");
        await host.DisposeAsync();

        var traces = collector.Requests.Where(r => r.Path == "/v1/traces").ToList();
        Assert.NotEmpty(traces);
        Assert.Contains("assessment.process", string.Concat(traces.Select(r => Encoding.UTF8.GetString(r.Body))), StringComparison.Ordinal);
    }

    public static TheoryData<string, string, string> ServiceNameSettings => new()
    {
        { "OTEL_SERVICE_NAME", "svc-by-name", "svc-by-name" },
        { "OTEL_RESOURCE_ATTRIBUTES", "deployment.environment=demo,service.name=svc-by-attributes", "svc-by-attributes" },
    };

    // Review gate: a service name from OTEL_SERVICE_NAME or from service.name in OTEL_RESOURCE_ATTRIBUTES is kept;
    // the default name does not override it.
    [Theory]
    [MemberData(nameof(ServiceNameSettings))]
    public async Task Configured_service_name_is_not_overridden_by_the_default(string key, string value, string expected)
    {
        using var collector = new FakeOtlpCollector();
        await using var factory = new ApiFactory();
        var host = ExportingHost(factory, ("OTEL_EXPORTER_OTLP_ENDPOINT", collector.Endpoint), (key, value));

        Assert.Equal(HttpStatusCode.OK, (await host.CreateClient().GetPathAsync("/health")).StatusCode);
        await collector.WaitForAsync(r => Text(r).Contains(expected, StringComparison.Ordinal), $"the service name {expected}");
        await host.DisposeAsync();

        var exported = Text(collector.Requests);
        Assert.Contains(expected, exported, StringComparison.Ordinal);
        Assert.DoesNotContain("mortgage-applications", exported, StringComparison.Ordinal);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ExportingHost(
        ApiFactory factory, params (string Key, string Value)[] extra)
    {
        // Short export intervals (traces, logs, metrics; package README "Environment Variables"), so data leaves while
        // the host runs and the tests do not depend on the flush at shutdown.
        var settings = new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            ["OTEL_BSP_SCHEDULE_DELAY"] = "100",
            ["OTEL_BLRP_SCHEDULE_DELAY"] = "100",
            ["OTEL_METRIC_EXPORT_INTERVAL"] = "100",
        };
        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        return factory.WithWebHostBuilder(
            builder =>
            {
                foreach (var (key, value) in settings)
                {
                    builder.UseSetting(key, value);
                }
            },
            runWorker: true);
    }

    private static string Text(IEnumerable<(string Path, byte[] Body)> requests) =>
        string.Concat(requests.Select(r => Encoding.UTF8.GetString(r.Body)));

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

        /// <summary>Waits (bounded) until the received requests satisfy <paramref name="condition"/>.</summary>
        public Task WaitForAsync(Func<IReadOnlyList<(string Path, byte[] Body)>, bool> condition, string what) =>
            Http.EventuallyAsync(
                () => Task.FromResult(condition(Requests)),
                () => $"the collector to receive {what}; received {_requests.Count} request(s) on "
                    + string.Join(", ", Requests.Select(r => r.Path).Distinct()));

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
