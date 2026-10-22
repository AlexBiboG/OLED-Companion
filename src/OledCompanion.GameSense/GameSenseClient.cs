using System.Net.Http.Json;

namespace OledCompanion.GameSense;

public sealed class GameSenseClient(
    HttpClient httpClient,
    CorePropsDiscovery discovery,
    GameSenseOptions? options = null)
{
    private readonly GameSenseOptions _options = options ?? new();
    private Uri? _engineBaseUri;

    public Task<GameSenseRegistrationResult> RegisterAsync(
        string? corePropsPath = null,
        CancellationToken cancellationToken = default) =>
        RegisterEventAsync(
            _options.Event,
            BuildScreenHandler(new Dictionary<string, object?>
            {
                ["has-text"] = true,
                ["context-frame-key"] = "custom-text"
            }),
            minValue: 0,
            maxValue: 1,
            valueOptional: true,
            corePropsPath,
            cancellationToken);

    public Task<GameSenseRegistrationResult> RegisterDiagnosticAsync(
        OledDiagnosticMode mode,
        string? corePropsPath = null,
        CancellationToken cancellationToken = default)
    {
        var definition = GetDiagnosticDefinition(mode);
        return RegisterEventAsync(
            definition.Event,
            definition.Handler,
            definition.MinValue,
            definition.MaxValue,
            definition.ValueOptional,
            corePropsPath,
            cancellationToken);
    }

    public Task<GameSenseHttpResult> ShowTextAsync(string text, CancellationToken cancellationToken = default)
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

    public Task<GameSenseHttpResult> SendDiagnosticEventAsync(
        OledDiagnosticMode mode,
        int sequence = 1,
        CancellationToken cancellationToken = default)
    {
        EnsureRegistered();
        var definition = GetDiagnosticDefinition(mode);
        return PostAsync("game_event", new Dictionary<string, object?>
        {
            ["game"] = _options.Game,
            ["event"] = definition.Event,
            ["data"] = definition.CreateData(sequence)
        }, cancellationToken);
    }

    public Task<GameSenseHttpResult> SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        EnsureRegistered();
        return PostAsync("game_heartbeat", new Dictionary<string, object?>
        {
            ["game"] = _options.Game
        }, cancellationToken);
    }

    private async Task<GameSenseRegistrationResult> RegisterEventAsync(
        string eventName,
        Dictionary<string, object?> handler,
        int minValue,
        int maxValue,
        bool valueOptional,
        string? corePropsPath,
        CancellationToken cancellationToken)
    {
        EnsureOledDeviceType();
        var props = await discovery.DiscoverAsync(corePropsPath, cancellationToken);
        _engineBaseUri = new Uri($"http://{props.Address.TrimEnd('/')}/", UriKind.Absolute);

        var metadata = await PostAsync("game_metadata", new Dictionary<string, object?>
        {
            ["game"] = _options.Game,
            ["game_display_name"] = _options.DisplayName,
            ["developer"] = _options.Developer
        }, cancellationToken);

        var screenHandler = await PostAsync("bind_game_event", new Dictionary<string, object?>
        {
            ["game"] = _options.Game,
            ["event"] = eventName,
            ["min_value"] = minValue,
            ["max_value"] = maxValue,
            ["value_optional"] = valueOptional,
            ["handlers"] = new[] { handler }
        }, cancellationToken);

        return new GameSenseRegistrationResult(metadata, screenHandler);
    }

    private async Task<GameSenseHttpResult> PostAsync(
        string endpoint,
        object payload,
        CancellationToken cancellationToken)
    {
        EnsureRegistered();
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                new Uri(_engineBaseUri!, endpoint), payload, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var reason = response.ReasonPhrase ?? "";
            if (!response.IsSuccessStatusCode)
                throw new GameSenseHttpException(endpoint, response.StatusCode, reason, responseBody);

            return new GameSenseHttpResult(endpoint, response.StatusCode, reason, responseBody);
        }
        catch (GameSenseUnavailableException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new GameSenseUnavailableException(
                "GameSense Engine could not be reached.", exception, isTransient: true);
        }
    }

    private Dictionary<string, object?> BuildScreenHandler(Dictionary<string, object?> frame) =>
        new()
        {
            ["device-type"] = _options.ScreenDeviceType,
            ["mode"] = "screen",
            ["zone"] = "one",
            ["datas"] = new[] { frame }
        };

    private DiagnosticDefinition GetDiagnosticDefinition(OledDiagnosticMode mode) => mode switch
    {
        OledDiagnosticMode.StaticPrefix => new(
            "OLED_DIAG_A", 1, 1, false,
            BuildScreenHandler(new Dictionary<string, object?>
            {
                ["has-text"] = true,
                ["prefix"] = "A STATIC: OLED-A",
                ["arg"] = ""
            }),
            _ => new Dictionary<string, object?> { ["value"] = 1 }),

        OledDiagnosticMode.DynamicContext => new(
            "OLED_DIAG_B", 0, 1, true,
            BuildScreenHandler(new Dictionary<string, object?>
            {
                ["has-text"] = true,
                ["context-frame-key"] = "diagnostic-text",
                ["length-millis"] = 0
            }),
            sequence => new Dictionary<string, object?>
            {
                ["frame"] = new Dictionary<string, string>
                {
                    ["diagnostic-text"] = $"B DYNAMIC {sequence:000}"
                }
            }),

        OledDiagnosticMode.NumericValue => new(
            "OLED_DIAG_C", 0, 999, false,
            BuildScreenHandler(new Dictionary<string, object?>
            {
                ["has-text"] = true,
                ["prefix"] = "C VALUE: "
            }),
            sequence => new Dictionary<string, object?> { ["value"] = 700 + sequence }),

        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown OLED diagnostic mode.")
    };

    private void EnsureOledDeviceType()
    {
        if (_options.ScreenDeviceType is not ("screened" or "screened-128x40"))
            throw new ArgumentException("Only screened or screened-128x40 OLED device types are allowed.", nameof(_options));
    }

    private void EnsureRegistered()
    {
        if (_engineBaseUri is null)
            throw new GameSenseUnavailableException("GameSense client is not registered.");
    }

    private sealed record DiagnosticDefinition(
        string Event,
        int MinValue,
        int MaxValue,
        bool ValueOptional,
        Dictionary<string, object?> Handler,
        Func<int, Dictionary<string, object?>> CreateData);
}
