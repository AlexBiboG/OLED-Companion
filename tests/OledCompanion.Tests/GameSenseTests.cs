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
        Assert.Equal("screened", binding.RootElement.GetProperty("handlers")[0].GetProperty("device-type").GetString());
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
