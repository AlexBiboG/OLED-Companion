using OledCompanion.GameSense;
using Xunit;

namespace OledCompanion.Tests;

public sealed class OledDemoRunnerTests
{
    [Fact]
    public async Task DemoShowsReadyModuleTimerAndActiveInOrder()
    {
        var display = new FakeDisplayService();
        var logs = new List<string>();
        var timings = new OledDemoTimings(
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(15),
            TimeSpan.FromMilliseconds(2));
        using var cancellation = new CancellationTokenSource();
        var runner = new OledDemoRunner(display, logs.Add, timings);
        var runTask = runner.RunAsync(cancellation.Token);

        await display.ActiveShown.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(new[] { "OLED READY", "MODULE TEST" }, display.ModuleTexts.Take(2));
        Assert.Contains(display.ModuleTexts, text => text.StartsWith("TIMER ", StringComparison.Ordinal));
        Assert.Contains("OLED ACTIVE", display.TimedTexts);
        Assert.Contains(logs, log => log.Contains("Modulwechsel", StringComparison.Ordinal));
    }

    private sealed class FakeDisplayService : IOledDisplayService
    {
        public List<string> ModuleTexts { get; } = [];
        public List<string> TimedTexts { get; } = [];
        public TaskCompletionSource<bool> ActiveShown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetModuleTextAsync(string moduleId, string text, CancellationToken cancellationToken = default)
        {
            ModuleTexts.Add(text);
            return Task.CompletedTask;
        }

        public async Task ShowModuleTextForAsync(
            string moduleId,
            string text,
            TimeSpan duration,
            CancellationToken cancellationToken = default)
        {
            TimedTexts.Add(text);
            ActiveShown.TrySetResult(true);
            await Task.Delay(duration, cancellationToken);
        }
    }
}
