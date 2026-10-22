using System.Net;

namespace OledCompanion.GameSense;

public sealed record GameSenseOptions(
    string Game = "OLED_COMPANION",
    string DisplayName = "OLED Companion",
    string Developer = "OLED Companion",
    string Event = "OLED_DISPLAY",
    string ScreenDeviceType = "screened-128x40");

public sealed record GameSenseHttpResult(
    string Endpoint,
    HttpStatusCode StatusCode,
    string ReasonPhrase,
    string ResponseBody)
{
    public string DiagnosticSummary => $"{Endpoint}: HTTP {(int)StatusCode} {ReasonPhrase}; response={ResponseBody}";
}

public sealed record GameSenseRegistrationResult(
    GameSenseHttpResult Metadata,
    GameSenseHttpResult ScreenHandler);

public enum OledDiagnosticMode
{
    StaticPrefix,
    DynamicContext,
    NumericValue
}

public class GameSenseUnavailableException(
    string message,
    Exception? inner = null,
    bool isTransient = false)
    : Exception(message, inner)
{
    public bool IsTransient { get; } = isTransient;
}

public sealed class GameSenseHttpException : GameSenseUnavailableException
{
    public GameSenseHttpException(
        string endpoint,
        HttpStatusCode statusCode,
        string reasonPhrase,
        string responseBody)
        : base(
            $"GameSense endpoint '{endpoint}' returned {(int)statusCode} {reasonPhrase}. Response: {responseBody}",
            isTransient: statusCode is HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string Endpoint { get; }
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }
}

public sealed record CoreProps(string Address);
