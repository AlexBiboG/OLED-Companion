using System.Diagnostics;
using OledCompanion.GameSense;

namespace OledCompanion.Modules;

public sealed record DetectedGame(
    string Id,
    string DisplayName,
    string ProcessName,
    int Priority,
    string ModuleId,
    string DisplayText);

public sealed record GameDetectionState(DetectedGame? ActiveGame)
{
    public static GameDetectionState None { get; } = new((DetectedGame?)null);
}

public interface IRunningProcessProvider
{
    IReadOnlyCollection<string> GetProcessNames();
}

public sealed class WindowsProcessProvider : IRunningProcessProvider
{
    public IReadOnlyCollection<string> GetProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                names.Add(Normalize(process.ProcessName));
            }
            catch (Exception) when (process.HasExited)
            {
                // A process can exit between enumeration and reading its name.
            }
            finally
            {
                process.Dispose();
            }
        }
        return names;
    }

    internal static string Normalize(string processName) =>
        Path.GetFileName(processName).Trim().TrimEnd('.').EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(processName).Trim()
            : $"{Path.GetFileName(processName).Trim()}.exe";
}

public sealed class GameDetectionService : IAsyncDisposable
{
    private readonly IOledDisplayService _display;
    private readonly OledClockModule _clock;
    private readonly IRunningProcessProvider _processProvider;
    private readonly IReadOnlyList<DetectedGame> _registrations;
    private readonly TimeSpan _interval;
    private readonly Func<bool> _clockEnabled;
    private readonly Action<string>? _logger;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private DetectedGame? _activeGame;

    public GameDetectionService(
        IOledDisplayService display,
        OledClockModule clock,
        IRunningProcessProvider processProvider,
        IEnumerable<DetectedGame> registrations,
        Func<bool> clockEnabled,
        TimeSpan? interval = null,
        Action<string>? logger = null)
    {
        _display = display;
        _clock = clock;
        _processProvider = processProvider;
        _registrations = registrations.OrderByDescending(game => game.Priority).ThenBy(game => game.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        _clockEnabled = clockEnabled;
        _interval = interval ?? TimeSpan.FromSeconds(3);
        _logger = logger;
        if (_registrations.Count == 0)
            throw new ArgumentException("At least one game registration is required.", nameof(registrations));
    }

    public bool Enabled { get; private set; }
    public DetectedGame? ActiveGame => _activeGame;
    public event EventHandler<GameDetectionState>? StateChanged;

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Enabled = enabled;
        if (!enabled)
        {
            _activeGame = null;
            await RestoreClockAsync(cancellationToken);
            PublishState();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_runTask is not null || !Enabled)
            return Task.CompletedTask;

        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = RunAsync(_runCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task EvaluateOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return;

        IReadOnlyCollection<string> processNames;
        try
        {
            processNames = _processProvider.GetProcessNames();
        }
        catch (Exception exception)
        {
            _logger?.Invoke($"Spielerkennung: Prozessabfrage fehlgeschlagen -> {exception.Message}");
            return;
        }

        var selected = _registrations.FirstOrDefault(game =>
            processNames.Any(process => string.Equals(
                WindowsProcessProvider.Normalize(process),
                WindowsProcessProvider.Normalize(game.ProcessName),
                StringComparison.OrdinalIgnoreCase)));

        if (string.Equals(_activeGame?.Id, selected?.Id, StringComparison.OrdinalIgnoreCase))
            return;

        if (selected is null)
        {
            await RestoreClockAsync(cancellationToken);
            _logger?.Invoke("Spielerkennung: kein registriertes Spiel; Uhr-Modul wiederhergestellt.");
        }
        else
        {
            try
            {
                await _clock.StopAsync();
                await _display.SetModuleTextAsync(selected.ModuleId, selected.DisplayText, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger?.Invoke($"Spielerkennung: OLED-Modulwechsel fehlgeschlagen -> {exception.Message}");
                return;
            }
            _logger?.Invoke($"Spielerkennung: {selected.DisplayName} aktiv -> {selected.DisplayText}");
        }
        _activeGame = selected;
        PublishState();
    }

    public async ValueTask DisposeAsync()
    {
        _runCancellation?.Cancel();
        if (_runTask is not null)
        {
            try { await _runTask; }
            catch (OperationCanceledException) { }
        }
        _runCancellation?.Dispose();
        _runCancellation = null;
        _runTask = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_interval);
            try
            {
                await EvaluateOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger?.Invoke($"Spielerkennung: Initialisierung fehlgeschlagen -> {exception.Message}");
            }
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await EvaluateOnceAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger?.Invoke($"Spielerkennung: Update fehlgeschlagen -> {exception.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger?.Invoke($"Spielerkennung beendet sich nach unerwartetem Fehler -> {exception.Message}");
        }
    }

    private async Task RestoreClockAsync(CancellationToken cancellationToken)
    {
        if (_clockEnabled() && !_clock.IsRunning)
            await _clock.StartAsync(cancellationToken);
    }

    private void PublishState() => StateChanged?.Invoke(this, new GameDetectionState(_activeGame));
}
