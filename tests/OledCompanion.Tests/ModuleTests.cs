using OledCompanion.GameSense;
using OledCompanion.Modules;
using Xunit;

namespace OledCompanion.Tests;

public sealed class ModuleTests
{
    [Fact]
    public async Task SettingsRoundTripUsesLocalJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oled-companion-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            var expected = new CompanionSettings(true, false, false, "C");
            await store.SaveAsync(expected);

            Assert.Equal(expected, await store.LoadAsync());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ClockModuleSendsInitialValueAndCanBeStoppedAndStartedAgain()
    {
        var display = new FakeDisplay();
        await using var clock = new OledClockModule(display);

        await clock.StartAsync();
        await WaitUntilAsync(() => display.Texts.Count == 1);
        await clock.DisposeAsync();

        await clock.StartAsync();
        await Task.Delay(50);

        Assert.All(display.Texts, text => Assert.Matches("^[0-9]{2}:[0-9]{2}$", text));
        Assert.Single(display.Texts);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
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
