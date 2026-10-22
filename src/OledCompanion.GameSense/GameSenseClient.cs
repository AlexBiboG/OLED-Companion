using System.Net.Http.Json;

namespace OledCompanion.GameSense;

public sealed class GameSenseClient(
    HttpClient httpClient,
    CorePropsDiscovery discovery,
    GameSenseOptions? options = null)
{
    private readonly GameSenseOptions _options = options ?? new();
    private Uri? _engineBaseUri;

    public async Task RegisterAsync(string? corePropsPath = null, CancellationToken cancellationToken = default)
    {
        var props = await discovery.DiscoverAsync(corePropsPath, cancellationToken);
        _engineBaseUri = new Uri($"http://{props.Address.TrimEnd('/')}/", UriKind.Absolute);

        await PostAsync("game_metadata", new Dictionary<string, object?>
        {
            ["game"] = _options.Game,
            ["game_display_name"] = _options.DisplayName,
            ["developer"] = _options.Developer
        }, cancellationToken);

        await PostAsync("bind_game_event", new Dictionary<string, object?>
        {
            ["game"] = _options.Game,
            ["event"] = _options.Event,
            ["min_value"] = 0,
            ["max_value"] = 1,
            ["value_optional"] = true,
            ["handlers"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["device-type"] = "screened",
                    ["mode"] = "screen",
                    ["zone"] = "one",
                    ["datas"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["has-text"] = true,
                            ["context-frame-key"] = "custom-text"
                        }
                    }
                }
            }
        }, cancellationToken);
    }

    public Task ShowTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureRegistered();
        return PostAsync("game_event", new Dictionary<string, object?>
        {
            ["game"] = _options.Game,
            ["event"] = _options.Event,
            ["data"] = new Dictionary<string, object?>
            {
                ["frame"] = new Dictionary<string, string> { ["custom-text"] = text }
            }
        }, cancellationToken);
    }

    private async Task PostAsync(string endpoint, object payload, CancellationToken cancellationToken)
    {
        EnsureRegistered();
        try
        {
            using var response = await httpClient.PostAsJsonAsync(new Uri(_engineBaseUri!, endpoint), payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new GameSenseUnavailableException($"GameSense endpoint '{endpoint}' returned {(int)response.StatusCode}.");
        }
        catch (GameSenseUnavailableException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new GameSenseUnavailableException("GameSense Engine could not be reached.", exception);
        }
    }

    private void EnsureRegistered()
    {
        if (_engineBaseUri is null)
            throw new GameSenseUnavailableException("GameSense client is not registered.");
    }
}
