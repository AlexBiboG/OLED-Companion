namespace OledCompanion.GameSense;

public interface IOledDisplayTransport
{
    Task<GameSenseRegistrationResult> ConnectAsync(CancellationToken cancellationToken = default);

    Task<GameSenseHttpResult> SendTextAsync(string text, CancellationToken cancellationToken = default);

    Task<GameSenseHttpResult> SendHeartbeatAsync(CancellationToken cancellationToken = default);
}

public sealed class GameSenseOledDisplayTransport(GameSenseClient client) : IOledDisplayTransport
{
    public Task<GameSenseRegistrationResult> ConnectAsync(CancellationToken cancellationToken = default) =>
        client.RegisterAsync(cancellationToken: cancellationToken);

    public Task<GameSenseHttpResult> SendTextAsync(string text, CancellationToken cancellationToken = default) =>
        client.ShowTextAsync(text, cancellationToken);

    public Task<GameSenseHttpResult> SendHeartbeatAsync(CancellationToken cancellationToken = default) =>
        client.SendHeartbeatAsync(cancellationToken);
}
