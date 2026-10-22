using OledCompanion.GameSense;
using OledCompanion.Modules;
using Xunit;

namespace OledCompanion.Tests;

public sealed class GameDetectionTests
{
    [Fact]
    public async Task ValorantProcessActivatesPlaceholderModule()
    {
        var provider = new FakeProcessProvider("VALORANT-Win64-Shipping.exe");
        var display = new FakeDisplay();
        await using var clock = new OledClockModule(display);
        await using var detection = CreateDetection(provider, display, clock);

        await detection.SetEnabledAsync(true);
        await detection.EvaluateOnceAsync();

        Assert.Equal("valorant", detection.ActiveGame?.Id);
        Assert.Equal("VALORANT ACTIVE", display.Texts.Single());
    }

    [Fact]
    public async Task EndingValorantRestoresClockModule()
    {
        var provider = new FakeProcessProvider("VALORANT-Win64-Shipping.exe");
        var display = new FakeDisplay();
        await using var clock = new OledClockModule(display);
        await clock.StartAsync();
        await WaitUntilAsync(() => display.Texts.Count == 1);
        await using var detection = CreateDetection(provider, display, clock);
        await detection.SetEnabledAsync(true);

        await detection.EvaluateOnceAsync();
        provider.Names.Clear();
        await detection.EvaluateOnceAsync();
        await WaitUntilAsync(() => display.Texts.Count == 3);

        Assert.Equal("VALORANT ACTIVE", display.Texts[1]);
        Assert.Matches("^[0-9]{2}:[0-9]{2}$", display.Texts[2]);
    }

    [Fact]
    public async Task UnchangedProcessStateDoesNotSwitchModuleAgain()
    {
        var provider = new FakeProcessProvider("VALORANT-Win64-Shipping.exe");
        var display = new FakeDisplay();
        await using var clock = new OledClockModule(display);
        await using var detection = CreateDetection(provider, display, clock);
        await detection.SetEnabledAsync(true);

        await detection.EvaluateOnceAsync();
        await detection.EvaluateOnceAsync();

        Assert.Single(display.Texts);
    }

    [Fact]
    public async Task HigherPriorityGameWinsDeterministically()
    {
        var provider = new FakeProcessProvider("shared-game.exe");
        var display = new FakeDisplay();
        await using var clock = new OledClockModule(display);
        await using var detection = new GameDetectionService(
            display,
            clock,
            provider,
            [
                new DetectedGame("low", "Low", "shared-game.exe", 10, "game:low", "LOW"),
                new DetectedGame("high", "High", "shared-game.exe", 20, "game:high", "HIGH")
            ],
            () => true);
        await detection.SetEnabledAsync(true);

        await detection.EvaluateOnceAsync();

        Assert.Equal("high", detection.ActiveGame?.Id);
        Assert.Equal("HIGH", display.Texts.Single());
    }

    [Fact]
    public async Task ProcessQueryFailureLeavesApplicationStable()
    {
        var display = new FakeDisplay();
        await using var clock = new OledClockModule(display);
        await using var detection = CreateDetection(new ThrowingProcessProvider(), display, clock);
        await detection.SetEnabledAsync(true);

        var exception = await Record.ExceptionAsync(() => detection.EvaluateOnceAsync());

        Assert.Null(exception);
        Assert.Null(detection.ActiveGame);
        Assert.Empty(display.Texts);
    }

    private static GameDetectionService CreateDetection(
        IRunningProcessProvider provider,
        IOledDisplayService display,
        OledClockModule clock) => new(
            display,
            clock,
            provider,
            [new DetectedGame("valorant", "VALORANT", "VALORANT-Win64-Shipping.exe", 100, "game:valorant", "VALORANT ACTIVE")],
            () => true);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class FakeProcessProvider(params string[] names) : IRunningProcessProvider
    {
        public List<string> Names { get; } = [.. names];
        public IReadOnlyCollection<string> GetProcessNames() => Names;
    }

    private sealed class ThrowingProcessProvider : IRunningProcessProvider
    {
        public IReadOnlyCollection<string> GetProcessNames() => throw new InvalidOperationException("test failure");
    }

    private sealed class FakeDisplay : IOledDisplayService
    {
        public List<string> Texts { get; } = [];
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetModuleTextAsync(string moduleId, string text, CancellationToken cancellationToken = default)
        {
            Texts.Add(text);
            return Task.CompletedTask;
        }
        public Task ShowModuleTextForAsync(string moduleId, string text, TimeSpan duration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
