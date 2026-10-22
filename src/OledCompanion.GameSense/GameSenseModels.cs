using System.Net;

namespace OledCompanion.GameSense;

public sealed record GameSenseOptions(
    string Game = "OLED_COMPANION",
    string DisplayName = "OLED Companion",
    string Developer = "OLED Companion",
    string Event = "OLED_DISPLAY",
    string ScreenDeviceType = "screened-128x40");

public sealed record GameSenseHttpResult(string Endpoint, HttpStatusCode StatusCode);

public sealed record GameSenseRegistrationResult(
    GameSenseHttpResult Metadata,
    GameSenseHttpResult ScreenHandler);

public sealed class GameSenseUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed record CoreProps(string Address);
