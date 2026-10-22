namespace OledCompanion.GameSense;

/// <summary>
/// Central OLED text service for future modules. It only uses the GameSense screen transport.
/// </summary>
public sealed class OledDisplayService(
    IOledDisplayTransport transport,
    TimeSpan? heartbeatInterval = null,
    TimeSpan? reconnectDelay = null,
    Action<string>? logger = null) : IOledDisplayService, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(5);
    private readonly TimeSpan _reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(1);
    private CancellationTokenSource? _runCancellation;
    private Task? _heartbeatTask;
    private bool _connected;
    private string? _currentText;
    private string _activeModule = "default";
    private bool _disposed;

    public bool IsConnected => _connected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_heartbeatTask is not null)
                return;

            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await ConnectLockedAsync(cancellationToken, restoreCurrentText: true);
            }
            catch (GameSenseUnavailableException exception) when (exception.IsTransient)
            {
                Log($"OLED: vorübergehender Startfehler; Reconnect läuft im Hintergrund -> {exception.Message}");
            }
            _heartbeatTask = HeartbeatLoopAsync(_runCancellation.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default) =>
        SetModuleTextAsync("default", text, cancellationToken);

    public async Task SetModuleTextAsync(
        string moduleId,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activeModule == moduleId && _currentText == text)
                return;

            // Keep the desired state even while GameSense is offline. The reconnect
            // loop will replay this latest value after registration succeeds.
            _activeModule = moduleId;
            _currentText = text;

            try
            {
                await SendTextWithReconnectLockedAsync(text, cancellationToken);
            }
            catch (GameSenseUnavailableException exception) when (exception.IsTransient)
            {
                _connected = false;
                Log($"OLED: Update vorübergehend nicht gesendet; letzter Text bleibt vorgemerkt -> {exception.Message}");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ShowTextForAsync(
        string text,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
        => await ShowModuleTextForAsync("default", text, duration, cancellationToken);

    public async Task ShowModuleTextForAsync(
        string moduleId,
        string text,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        ThrowIfDisposed();
        var previousText = _currentText;
        var previousModule = _activeModule;
        await SetModuleTextAsync(moduleId, text, cancellationToken);

        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                if (_currentText == text && _activeModule == moduleId && previousText is null)
                {
                    _currentText = " ";
                    try
                    {
                        await SendTextWithReconnectLockedAsync(" ", CancellationToken.None);
                    }
                    catch (GameSenseUnavailableException exception) when (exception.IsTransient)
                    {
                        _connected = false;
                        Log($"OLED: Anzeige konnte nicht geleert werden; Reconnect übernimmt -> {exception.Message}");
                    }
                }
                else if (_currentText == text && _activeModule == moduleId)
                {
                    _activeModule = previousModule;
                    _currentText = previousText!;
                    try
                    {
                        await SendTextWithReconnectLockedAsync(previousText!, CancellationToken.None);
                    }
                    catch (GameSenseUnavailableException exception) when (exception.IsTransient)
                    {
                        _connected = false;
                        Log($"OLED: Wiederherstellung vorübergehend nicht gesendet -> {exception.Message}");
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _runCancellation?.Cancel();
        if (_heartbeatTask is not null)
        {
            try
            {
                await _heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                // Never leak a background-task exception from disposal.
                Log($"OLED: Hintergrundtask beim Beenden beendet -> {exception.Message}");
            }
        }

        _runCancellation?.Dispose();
        _gate.Dispose();
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        var retryAttempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    if (_connected)
                    {
                        await transport.SendHeartbeatAsync(cancellationToken);
                    }
                    else
                    {
                        await ConnectLockedAsync(cancellationToken, restoreCurrentText: true);
                    }
                }
                finally
                {
                    _gate.Release();
                }

                retryAttempt = 0;
                await Task.Delay(_heartbeatInterval, cancellationToken);
            }
            catch (GameSenseUnavailableException exception) when (exception.IsTransient)
            {
                _connected = false;
                Log($"OLED: GameSense-Verbindung verloren; Reconnect wird versucht -> {exception.Message}");
                await DelayBeforeRetryAsync(retryAttempt++, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A transport or serialization defect must not fault the demo task.
                _connected = false;
                Log($"OLED: Unerwarteter Hintergrundfehler; Reconnect wird versucht -> {exception.Message}");
                await DelayBeforeRetryAsync(retryAttempt++, cancellationToken);
            }
        }
    }

    private async Task SendTextWithReconnectLockedAsync(
        string text,
        CancellationToken cancellationToken)
    {
        if (!_connected)
            await ConnectLockedAsync(cancellationToken, restoreCurrentText: false);

        try
        {
            await transport.SendTextAsync(text, cancellationToken);
        }
        catch (GameSenseUnavailableException exception) when (exception.IsTransient)
        {
            _connected = false;
            Log($"OLED: OLED-Update fehlgeschlagen; Reconnect wird versucht -> {exception.Message}");
            throw;
        }
        Log($"OLED: OLED-Update gesendet -> {text}");
    }

    private async Task DelayBeforeRetryAsync(int retryAttempt, CancellationToken cancellationToken)
    {
        var baseMilliseconds = Math.Max(1, _reconnectDelay.TotalMilliseconds);
        var multiplier = Math.Pow(2, Math.Min(retryAttempt, 8));
        var delayMilliseconds = Math.Min(5000, baseMilliseconds * multiplier);
        await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
    }

    private async Task ConnectLockedAsync(CancellationToken cancellationToken, bool restoreCurrentText)
    {
        Log(_connected ? "OLED: Reconnect zu GameSense ..." : "OLED: Verbindung zu GameSense ...");
        try
        {
            await transport.ConnectAsync(cancellationToken);
            _connected = true;
            Log("OLED: GameSense-Verbindung hergestellt.");
            if (restoreCurrentText && _currentText is not null)
            {
                await transport.SendTextAsync(_currentText, cancellationToken);
                Log($"OLED: letzter Text wiederhergestellt -> {_currentText}");
            }
        }
        catch (GameSenseUnavailableException exception)
        {
            _connected = false;
            Log($"OLED: Verbindung fehlgeschlagen -> {exception.Message}");
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OledDisplayService));
    }

    private void Log(string message) => logger?.Invoke(message);
}
