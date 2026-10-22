using OledCompanion.GameSense;

namespace OledCompanion.Modules;

public sealed class ValorantDashboardModule : IAsyncDisposable
{
    private readonly OledDisplayService _display;
    private readonly ISystemSensorProvider _sensors;
    private readonly OledWidgetRenderer _renderer;
    private WidgetPageConfiguration _page;
    private readonly Action<string>? _logger;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;

    public ValorantDashboardModule(
        OledDisplayService display,
        ISystemSensorProvider sensors,
        WidgetPageConfiguration? page = null,
        Action<string>? logger = null)
    {
        _display = display;
        _sensors = sensors;
        _renderer = new OledWidgetRenderer();
        _page = page ?? StandardPage;
        _logger = logger;
    }

    public OledFrame? LastFrame { get; private set; }
    public bool IsRunning => _runTask is not null;

    public void SetPage(WidgetPageConfiguration page) => _page = page;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_runTask is not null) return Task.CompletedTask;
        LastFrame = null;
        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = RunAsync(_runCancellation.Token);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sensors.Dispose();
    }

    public async Task StopAsync()
    {
        _runCancellation?.Cancel();
        if (_runTask is not null)
        {
            try { await _runTask; }
            catch (OperationCanceledException) { }
            _runTask = null;
        }
        _runCancellation?.Dispose();
        _runCancellation = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            await UpdateAsync(cancellationToken);
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await UpdateAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger?.Invoke($"Valorant-Dashboard: Fehler -> {exception.Message}");
        }
    }

    private async Task UpdateAsync(CancellationToken cancellationToken)
    {
        SensorSnapshot snapshot;
        try { snapshot = _sensors.ReadSnapshot(); }
        catch (Exception exception)
        {
            _logger?.Invoke($"Sensoren nicht verfügbar -> {exception.Message}");
            snapshot = SensorSnapshot.Empty;
        }

        var frame = _renderer.Render(_page, new WidgetRenderContext(DateTime.Now, snapshot, "VALORANT"));
        if (LastFrame is not null && LastFrame.Bytes.SequenceEqual(frame.Bytes))
            return;

        LastFrame = frame;
        try
        {
            await _display.SetFrameAsync("game:valorant", frame.Bytes, cancellationToken);
            _logger?.Invoke("Valorant-Dashboard: OLED-Frame aktualisiert.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.Invoke($"Valorant-Dashboard: Grafik nicht verfügbar -> {exception.Message}");
            await _display.SetModuleTextAsync("game:valorant", "VALORANT ACTIVE", cancellationToken);
        }
    }

    public static WidgetPageConfiguration StandardPage { get; } = new(
        "Valorant Standard",
        [
            new WidgetConfiguration("status", "status", 1, 1, 120, 7),
            new WidgetConfiguration("cpu", "cpu", 1, 11, 60, 7),
            new WidgetConfiguration("gpu", "gpu", 1, 21, 60, 7),
            new WidgetConfiguration("clock", "clock", 77, 11, 48, 7),
            new WidgetConfiguration("bar", "bar", 77, 23, 48, 7)
        ]);

    public static WidgetPageConfiguration MinimalPage { get; } = new(
        "Minimal",
        [
            new WidgetConfiguration("status", "status", 1, 1, 120, 7),
            new WidgetConfiguration("clock", "clock", 40, 18, 48, 7)
        ]);
}
