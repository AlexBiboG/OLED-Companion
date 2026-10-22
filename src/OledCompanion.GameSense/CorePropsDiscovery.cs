using System.Text.Json;

namespace OledCompanion.GameSense;

public sealed class CorePropsDiscovery
{
    public const string WindowsRelativePath = "SteelSeries/SteelSeries Engine 3/coreProps.json";

    public string GetDefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        WindowsRelativePath.Replace('/', Path.DirectorySeparatorChar));

    public async Task<CoreProps> DiscoverAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        var filePath = path ?? GetDefaultPath();
        if (!File.Exists(filePath))
            throw new GameSenseUnavailableException($"GameSense coreProps.json was not found: {filePath}");

        try
        {
            await using var stream = File.OpenRead(filePath);
            var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("address", out var addressElement) ||
                addressElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(addressElement.GetString()))
                throw new GameSenseUnavailableException("GameSense coreProps.json does not contain a valid address.");

            var address = addressElement.GetString()!;
            if (!Uri.TryCreate($"http://{address}", UriKind.Absolute, out var uri) || uri.Port <= 0)
                throw new GameSenseUnavailableException($"GameSense address is invalid: {address}");

            return new CoreProps(address);
        }
        catch (JsonException exception)
        {
            throw new GameSenseUnavailableException("GameSense coreProps.json is not valid JSON.", exception);
        }
        catch (IOException exception)
        {
            throw new GameSenseUnavailableException("GameSense coreProps.json could not be read.", exception);
        }
    }
}
