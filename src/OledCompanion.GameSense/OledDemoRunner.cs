using System.Diagnostics;

namespace OledCompanion.GameSense;

public sealed record OledDemoTimings(
    TimeSpan ReadyDuration,
    TimeSpan ModuleDuration,
    TimeSpan TimerTick,
    TimeSpan ActiveInterval,
    TimeSpan ActiveDuration)
{
    public static OledDemoTimings Default { get; } = new(
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(1));
}

public sealed class OledDemoRunner(
    IOledDisplayService display,
    Action<string>? logger = null,
    OledDemoTimings? timings = null)
{
    private const string ModuleId = "demo";
    private readonly OledDemoTimings _timings = timings ?? OledDemoTimings.Default;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await SetModuleTextAsync("OLED READY", cancellationToken);
        await Task.Delay(_timings.ReadyDuration, cancellationToken);

        await SetModuleTextAsync("MODULE TEST", cancellationToken);
        await Task.Delay(_timings.ModuleDuration, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var nextActiveAt = _timings.ActiveInterval;
        while (true)
        {
            var elapsedSeconds = (int)stopwatch.Elapsed.TotalSeconds;
            await SetModuleTextAsync($"TIMER {elapsedSeconds:00}", cancellationToken);
            await Task.Delay(_timings.TimerTick, cancellationToken);

            if (stopwatch.Elapsed >= nextActiveAt)
            {
                Log("Demo: OLED ACTIVE wird angezeigt.");
                await display.ShowModuleTextForAsync(
                    ModuleId,
                    "OLED ACTIVE",
                    _timings.ActiveDuration,
                    cancellationToken);
                Log("Demo: vorheriger Timer wurde wiederhergestellt.");
                nextActiveAt += _timings.ActiveInterval;
            }
        }
    }

    private async Task SetModuleTextAsync(string text, CancellationToken cancellationToken)
    {
        Log($"Demo: Modulwechsel/Anzeige -> {text}");
        await display.SetModuleTextAsync(ModuleId, text, cancellationToken);
        Log($"Demo: OLED-Update gesendet -> {text}");
    }

    private void Log(string message) => logger?.Invoke(message);
}
