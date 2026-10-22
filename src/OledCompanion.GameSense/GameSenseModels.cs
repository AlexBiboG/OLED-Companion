namespace OledCompanion.GameSense;

public sealed record GameSenseOptions(
    string Game = "OLED_COMPANION",
    string DisplayName = "OLED Companion",
    string Developer = "OLED Companion",
    string Event = "OLED_DISPLAY");

public sealed class GameSenseUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed record CoreProps(string Address);
