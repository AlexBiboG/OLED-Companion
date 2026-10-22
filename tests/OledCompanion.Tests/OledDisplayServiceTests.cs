using System.Net;
using OledCompanion.GameSense;
using Xunit;

namespace OledCompanion.Tests;

public sealed class OledDisplayServiceTests
{
    [Fact]
    public async Task IdenticalTextIsNotSentTwiceAndModuleSwitchIsSent()
    {
        var transport = new FakeTransport();
        await using var service = new OledDisplayService(transport);

        await service.SetTextAsync("READY");
        await service.SetTextAsync("READY");
        await service.SetModuleTextAsync("module-a", "READY");
        await service.SetModuleTextAsync("module-a", "READY");

        Assert.Equal(new[] { "READY", "READY" }, transport.Texts);
        Assert.Equal(1, transport.ConnectCount);
    }

    [Fact]
    public async Task TimedTextRestoresPreviousText()
    {
        var transport = new FakeTransport();
        await using var service = new OledDisplayService(transport);

        await service.SetTextAsync("BASE");
        await service.ShowTextForAsync("TEMP", TimeSpan.FromMilliseconds(10));

        Assert.Equal(new[] { "BASE", "TEMP", "BASE" }, transport.Texts);
    }

    [Fact]
    public async Task HeartbeatFailureReconnectsAndRestoresCurrentText()
    {
        var transport = new FakeTransport { FailNextHeartbeat = true };
        await using var service = new OledDisplayService(
            transport,
            heartbeatInterval: TimeSpan.FromMilliseconds(10),
            reconnectDelay: TimeSpan.FromMilliseconds(1));

        await service.StartAsync();
        await service.SetTextAsync("PERSIST");
        await transport.Reconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(transport.HeartbeatCount >= 1);
        Assert.True(transport.ConnectCount >= 2);
        Assert.Contains("PERSIST", transport.Texts);
    }

    [Fact]
    public async Task InitialConnectionFailureIsReported()
    {
        var transport = new FakeTransport { FailConnect = true };
        await using var service = new OledDisplayService(transport);

        var exception = await Assert.ThrowsAsync<GameSenseUnavailableException>(() => service.SetTextAsync("FAIL"));

        Assert.Contains("connect", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeTransport : IOledDisplayTransport
    {
        public List<string> Texts { get; } = [];
        public int ConnectCount { get; private set; }
        public int HeartbeatCount { get; private set; }
        public bool FailConnect { get; init; }
        public bool FailNextHeartbeat { get; set; }
        public TaskCompletionSource<bool> Reconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<GameSenseRegistrationResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectCount++;
            if (FailConnect)
                throw new GameSenseUnavailableException("connect failed");
            if (ConnectCount >= 2)
                Reconnected.TrySetResult(true);
            return Task.FromResult(new GameSenseRegistrationResult(Result("game_metadata"), Result("bind_game_event")));
        }

        public Task<GameSenseHttpResult> SendTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Texts.Add(text);
            return Task.FromResult(Result("game_event"));
        }

        public Task<GameSenseHttpResult> SendHeartbeatAsync(CancellationToken cancellationToken = default)
        {
            HeartbeatCount++;
            if (FailNextHeartbeat)
            {
                FailNextHeartbeat = false;
                throw new GameSenseUnavailableException("heartbeat failed");
            }
            return Task.FromResult(Result("game_heartbeat"));
        }

        private static GameSenseHttpResult Result(string endpoint) =>
            new(endpoint, HttpStatusCode.OK, "OK", "{}");
    }
}
