using OledCompanion.GameSense;

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
var discovery = new CorePropsDiscovery();
var client = new GameSenseClient(httpClient, discovery);
using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
    Console.WriteLine("Strg+C erkannt – Testmodus wird sauber beendet ...");
};

Console.WriteLine("OLED Companion – Windows-Hardwaretest");
Console.WriteLine("SteelSeries Engine: Suche coreProps.json ...");

try
{
    var props = await discovery.DiscoverAsync();
    Console.WriteLine($"SteelSeries Engine: gefunden ({props.Address})");
}
catch (GameSenseUnavailableException exception)
{
    Console.Error.WriteLine("SteelSeries Engine: nicht gefunden");
    Console.Error.WriteLine($"Fehler: {exception.Message}");
    return 1;
}

try
{
    var registration = await client.RegisterAsync(cancellationToken: cancellation.Token);
    Console.WriteLine($"GameSense-Endpunkt: erreichbar (HTTP {(int)registration.Metadata.StatusCode})");
    Console.WriteLine($"Registrierung: erfolgreich (HTTP {(int)registration.Metadata.StatusCode})");
    Console.WriteLine($"OLED-Handler: screened-128x40 / one (HTTP {(int)registration.ScreenHandler.StatusCode})");

    try
    {
        var oledEvent = await client.ShowTextAsync("OLED COMPANION", cancellation.Token);
        Console.WriteLine($"OLED-Event: gesendet (HTTP {(int)oledEvent.StatusCode}, OLED COMPANION)");

        Console.WriteLine("Dauerhafter OLED-Testmodus aktiv. Heartbeat alle 5 Sekunden; Strg+C zum Beenden.");
        using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await heartbeat.WaitForNextTickAsync(cancellation.Token))
        {
            try
            {
                var result = await client.SendHeartbeatAsync(cancellation.Token);
                Console.WriteLine($"GameSense-Heartbeat: gesendet (HTTP {(int)result.StatusCode})");
            }
            catch (GameSenseUnavailableException exception)
            {
                Console.Error.WriteLine($"GameSense-Heartbeat: fehlgeschlagen – {exception.Message}");
            }
        }
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    {
        Console.WriteLine("OLED-Testmodus beendet.");
    }
    catch (GameSenseUnavailableException exception)
    {
        Console.Error.WriteLine("OLED-Event: fehlgeschlagen");
        Console.Error.WriteLine($"Fehler: {exception.Message}");
        return 1;
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("OLED-Testmodus beendet.");
}
catch (GameSenseUnavailableException exception)
{
    if (exception.Message.Contains("could not be reached", StringComparison.OrdinalIgnoreCase))
        Console.Error.WriteLine("GameSense-Endpunkt: nicht erreichbar");
    else
        Console.WriteLine("GameSense-Endpunkt: erreichbar");

    Console.Error.WriteLine("Registrierung: fehlgeschlagen");
    Console.Error.WriteLine($"Fehler: {exception.Message}");
    return 1;
}

return 0;
