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

            await ConnectLockedAsync(cancellationToken, restoreCurrentText: true);
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
            if (_connected && _activeModule == moduleId && _currentText == text)
                return;

            await SendTextWithReconnectLockedAsync(moduleId, text, cancellationToken);
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
                    await SendTextWithReconnectLockedAsync(moduleId, " ", CancellationToken.None);
                }
                else if (_currentText == text && _activeModule == moduleId)
                {
                    await SendTextWithReconnectLockedAsync(previousModule, previousText!, CancellationToken.None);
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
        }

        _runCancellation?.Dispose();
        _gate.Dispose();
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
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
                catch (GameSenseUnavailableException)
                {
                    _connected = false;
                    Log("OLED: GameSense-Verbindung verloren; Reconnect wird versucht.");
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!_connected)
            {
                try
                {
                    await Task.Delay(_reconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task SendTextWithReconnectLockedAsync(
        string moduleId,
        string text,
        CancellationToken cancellationToken)
    {
        if (!_connected)
            await ConnectLockedAsync(cancellationToken, restoreCurrentText: false);

        try
        {
            await transport.SendTextAsync(text, cancellationToken);
        }
        catch (GameSenseUnavailableException)
        {
            _connected = false;
            await ConnectLockedAsync(cancellationToken, restoreCurrentText: false);
            await transport.SendTextAsync(text, cancellationToken);
        }

        _activeModule = moduleId;
        _currentText = text;
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
