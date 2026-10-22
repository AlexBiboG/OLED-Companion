using System.Text.Json;
using OledCompanion.GameSense;

namespace OledCompanion.Modules;

public sealed record CompanionSettings(
    bool StartWithWindows = false,
    bool RunInBackground = true,
    bool ClockModuleEnabled = true,
    string DiagnosticMode = "B");

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<CompanionSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return new CompanionSettings();

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<CompanionSettings>(stream, JsonOptions, cancellationToken)
                ?? new CompanionSettings();
        }
        catch (JsonException)
        {
            return new CompanionSettings();
        }
        catch (IOException)
        {
            return new CompanionSettings();
        }
    }

    public async Task SaveAsync(CompanionSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }
}

public sealed class OledClockModule(
    IOledDisplayService display,
    Action<string>? logger = null) : IAsyncDisposable
{
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private string? _lastText;

    public bool IsRunning => _runTask is not null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_runTask is not null)
            return Task.CompletedTask;

        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linked = _runCancellation;
        _runTask = RunAsync(linked);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
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

    private async Task RunAsync(CancellationTokenSource linked)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            await UpdateAsync(linked.Token);
            while (await timer.WaitForNextTickAsync(linked.Token))
                await UpdateAsync(linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger?.Invoke($"Uhr-Modul: Fehler -> {exception.Message}");
        }
        finally
        {
            linked.Dispose();
        }
    }

    private async Task UpdateAsync(CancellationToken cancellationToken)
    {
        var text = DateTime.Now.ToString("HH:mm");
        if (text == _lastText)
            return;

        _lastText = text;
        await display.SetModuleTextAsync("clock", text, cancellationToken);
        logger?.Invoke($"Uhr-Modul: OLED-Update -> {text}");
    }
}
