using OledCompanion.GameSense;

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
var discovery = new CorePropsDiscovery();
var client = new GameSenseClient(httpClient, discovery);

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
    await client.RegisterAsync();
    Console.WriteLine("GameSense-Endpunkt: erreichbar");
    Console.WriteLine("Registrierung: erfolgreich");

    try
    {
        await client.ShowTextAsync("OLED COMPANION");
        Console.WriteLine("OLED-Event: gesendet (OLED COMPANION)");
    }
    catch (GameSenseUnavailableException exception)
    {
        Console.Error.WriteLine("OLED-Event: fehlgeschlagen");
        Console.Error.WriteLine($"Fehler: {exception.Message}");
        return 1;
    }
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
