using System.Net;
using System.Text.Json;
using OledCompanion.GameSense;
using Xunit;

namespace OledCompanion.Tests;

public sealed class GameSenseTests
{
    [Fact]
    public async Task DiscoveryReadsAddressFromCoreProps()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "{\"address\":\"127.0.0.1:51248\"}");

        var result = await new CorePropsDiscovery().DiscoverAsync(path);

        Assert.Equal("127.0.0.1:51248", result.Address);
        File.Delete(path);
    }

    [Fact]
    public async Task MissingCorePropsIsReportedAsUnavailable()
    {
        var exception = await Assert.ThrowsAsync<GameSenseUnavailableException>(() =>
            new CorePropsDiscovery().DiscoverAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task RegistrationAndDemoSendOfficialPayloadShapes()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var corePropsPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(corePropsPath, "{\"address\":\"127.0.0.1:51248\"}");

        var gameSense = new GameSenseClient(client, new CorePropsDiscovery());
        await gameSense.RegisterAsync(corePropsPath);
        await gameSense.ShowTextAsync("OLED COMPANION");

        Assert.Equal(new[] { "/game_metadata", "/bind_game_event", "/game_event" }, handler.Paths);
        using var binding = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("OLED_COMPANION", binding.RootElement.GetProperty("game").GetString());
        Assert.Equal("OLED_DISPLAY", binding.RootElement.GetProperty("event").GetString());
        Assert.Equal("screened-128x40", binding.RootElement.GetProperty("handlers")[0].GetProperty("device-type").GetString());
        Assert.DoesNotContain("rgb", binding.RootElement.GetProperty("handlers").GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var eventPayload = JsonDocument.Parse(handler.Bodies[2]);
        Assert.Equal("OLED COMPANION", eventPayload.RootElement.GetProperty("data").GetProperty("frame").GetProperty("custom-text").GetString());
        File.Delete(corePropsPath);
    }

    [Fact]
    public async Task HttpFailureIsReportedAsUnavailable()
    {
        var handler = new RecordingHandler { ExceptionToThrow = new HttpRequestException("connection refused") };
        using var client = new HttpClient(handler);
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "{\"address\":\"127.0.0.1:51248\"}");

        var exception = await Assert.ThrowsAsync<GameSenseUnavailableException>(() =>
            new GameSenseClient(client, new CorePropsDiscovery()).RegisterAsync(path));

        Assert.Contains("could not be reached", exception.Message);
        File.Delete(path);
    }

    [Fact]
    public async Task HeartbeatUsesOnlyGameNameAndTheOledHandlerIsScreenOnly()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "{\"address\":\"127.0.0.1:51248\"}");

        var gameSense = new GameSenseClient(client, new CorePropsDiscovery());
        await gameSense.RegisterAsync(path);
        await gameSense.SendHeartbeatAsync();

        Assert.Equal("/game_heartbeat", handler.Paths[2]);
        using var heartbeat = JsonDocument.Parse(handler.Bodies[2]);
        Assert.Equal("OLED_COMPANION", heartbeat.RootElement.GetProperty("game").GetString());
        Assert.DoesNotContain("handler", heartbeat.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        File.Delete(path);
    }

    [Fact]
    public async Task NonOledDeviceTypesAreRejected()
    {
        var options = new GameSenseOptions(ScreenDeviceType: "rgb-per-key-zones");
        using var client = new HttpClient(new RecordingHandler());
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "{\"address\":\"127.0.0.1:51248\"}");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new GameSenseClient(client, new CorePropsDiscovery(), options).RegisterAsync(path));
        File.Delete(path);
    }

    [Fact]
    public async Task AllDiagnosticModesUseDistinctOledOnlyPayloads()
    {
        foreach (var mode in Enum.GetValues<OledDiagnosticMode>())
        {
            var handler = new RecordingHandler();
            using var httpClient = new HttpClient(handler);
            var path = Path.GetTempFileName();
            await File.WriteAllTextAsync(path, "{\"address\":\"127.0.0.1:51248\"}");

            var gameSense = new GameSenseClient(httpClient, new CorePropsDiscovery());
            var registration = await gameSense.RegisterDiagnosticAsync(mode, path);
            var eventResult = await gameSense.SendDiagnosticEventAsync(mode, 7);

            Assert.Equal(HttpStatusCode.OK, registration.ScreenHandler.StatusCode);
            Assert.Equal(HttpStatusCode.OK, eventResult.StatusCode);
            using var binding = JsonDocument.Parse(handler.Bodies[1]);
            var handlerJson = binding.RootElement.GetProperty("handlers")[0];
            Assert.Equal("screened-128x40", handlerJson.GetProperty("device-type").GetString());
            Assert.Equal("screen", handlerJson.GetProperty("mode").GetString());
            Assert.Equal("one", handlerJson.GetProperty("zone").GetString());
            Assert.DoesNotContain("rgb", handlerJson.GetRawText(), StringComparison.OrdinalIgnoreCase);

            using var eventPayload = JsonDocument.Parse(handler.Bodies[2]);
            Assert.Equal(mode switch
            {
                OledDiagnosticMode.StaticPrefix => "OLED_DIAG_A",
                OledDiagnosticMode.DynamicContext => "OLED_DIAG_B",
                OledDiagnosticMode.NumericValue => "OLED_DIAG_C",
                _ => ""
            }, eventPayload.RootElement.GetProperty("event").GetString());

            var data = eventPayload.RootElement.GetProperty("data");
            switch (mode)
            {
                case OledDiagnosticMode.StaticPrefix:
                    Assert.Equal(1, data.GetProperty("value").GetInt32());
                    Assert.DoesNotContain("frame", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
                    break;
                case OledDiagnosticMode.DynamicContext:
                    Assert.Equal("B DYNAMIC 007", data.GetProperty("frame").GetProperty("diagnostic-text").GetString());
                    break;
                case OledDiagnosticMode.NumericValue:
                    Assert.Equal(707, data.GetProperty("value").GetInt32());
                    break;
            }

            File.Delete(path);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string> Bodies { get; } = [];
        public Exception? ExceptionToThrow { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            Paths.Add(request.RequestUri!.AbsolutePath);
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
