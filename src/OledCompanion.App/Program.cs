using OledCompanion.GameSense;

if (args.Length == 1 && args[0].Equals("demo", StringComparison.OrdinalIgnoreCase))
    return await RunDemoAsync();

if (!TryParseMode(args, out var mode))
{
    Console.Error.WriteLine("Verwendung: OledCompanion.App.exe demo|A|B|C");
    Console.Error.WriteLine("demo = Live-Engine-Demo mit OledDisplayService");
    Console.Error.WriteLine("A = statischer Handler-Text, B = context-frame-key, C = numerischer data.value");
    return 2;
}

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
var discovery = new CorePropsDiscovery();
var client = new GameSenseClient(httpClient, discovery);
using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
    Console.WriteLine("Strg+C erkannt – Diagnosemodus wird sauber beendet ...");
};

Console.WriteLine($"OLED Companion – OLED-Diagnosemodus {ModeName(mode)}");
Console.WriteLine("SteelSeries Engine: Suche coreProps.json ...");

try
{
    var props = await discovery.DiscoverAsync(cancellationToken: cancellation.Token);
    Console.WriteLine($"SteelSeries Engine: gefunden ({props.Address})");

    var registration = await client.RegisterDiagnosticAsync(mode, cancellationToken: cancellation.Token);
    LogResult("game_metadata", registration.Metadata);
    LogResult("OLED-Handler / bind_game_event", registration.ScreenHandler);

    var sequence = 1;
    var firstEvent = await client.SendDiagnosticEventAsync(mode, sequence, cancellation.Token);
    LogResult($"OLED-Testevent {ModeName(mode)}", firstEvent);

    var interval = mode == OledDiagnosticMode.StaticPrefix
        ? TimeSpan.FromSeconds(10)
        : TimeSpan.FromSeconds(5);
    Console.WriteLine($"Diagnosemodus aktiv. Intervall: {interval.TotalSeconds:0} Sekunden; Strg+C zum Beenden.");
    using var timer = new PeriodicTimer(interval);
    while (await timer.WaitForNextTickAsync(cancellation.Token))
    {
        try
        {
            var result = mode == OledDiagnosticMode.StaticPrefix
                ? await client.SendHeartbeatAsync(cancellation.Token)
                : await client.SendDiagnosticEventAsync(mode, ++sequence, cancellation.Token);
            LogResult(mode == OledDiagnosticMode.StaticPrefix ? "game_heartbeat" : $"OLED-Testevent {ModeName(mode)}", result);
        }
        catch (GameSenseUnavailableException exception)
        {
            Console.Error.WriteLine($"HTTP-Fehler im Diagnosemodus: {exception.Message}");
        }
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("OLED-Diagnosemodus beendet.");
}
catch (GameSenseUnavailableException exception)
{
    Console.Error.WriteLine($"OLED-Diagnose fehlgeschlagen: {exception.Message}");
    return 1;
}

return 0;

static void LogResult(string operation, GameSenseHttpResult result) =>
    Console.WriteLine($"{operation}: {result.DiagnosticSummary}");

static bool TryParseMode(string[] args, out OledDiagnosticMode mode)
{
    mode = default;
    if (args.Length != 1)
        return false;

    mode = args[0].ToUpperInvariant() switch
    {
        "A" => OledDiagnosticMode.StaticPrefix,
        "B" => OledDiagnosticMode.DynamicContext,
        "C" => OledDiagnosticMode.NumericValue,
        _ => (OledDiagnosticMode)(-1)
    };
    return mode is OledDiagnosticMode.StaticPrefix or OledDiagnosticMode.DynamicContext or OledDiagnosticMode.NumericValue;
}

static string ModeName(OledDiagnosticMode mode) => mode switch
{
    OledDiagnosticMode.StaticPrefix => "A / STATIC PREFIX",
    OledDiagnosticMode.DynamicContext => "B / DYNAMIC CONTEXT",
    OledDiagnosticMode.NumericValue => "C / NUMERIC VALUE",
    _ => "UNKNOWN"
};

static async Task<int> RunDemoAsync()
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var client = new GameSenseClient(httpClient, new CorePropsDiscovery());
    await using var display = new OledDisplayService(
        new GameSenseOledDisplayTransport(client),
        logger: Console.WriteLine);
    using var cancellation = new CancellationTokenSource();

    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
        Console.WriteLine("Strg+C erkannt – Demo wird sauber beendet ...");
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        Console.WriteLine("OLED Companion – Live-Engine-Demo");
        await display.StartAsync(cancellation.Token);
        var runner = new OledDemoRunner(display, Console.WriteLine);
        await runner.RunAsync(cancellation.Token);
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    {
        Console.WriteLine("Live-Engine-Demo beendet.");
    }
    catch (GameSenseUnavailableException exception)
    {
        Console.Error.WriteLine($"Live-Engine-Demo fehlgeschlagen: {exception.Message}");
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }

    return 0;
}
