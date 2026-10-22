namespace OledCompanion.Modules;

public sealed record SensorSnapshot(
    double? CpuTemperature,
    IReadOnlyList<(string Name, double? Temperature)> GpuTemperatures)
{
    public static SensorSnapshot Empty { get; } = new(null, []);
}

public interface ISystemSensorProvider : IDisposable
{
    SensorSnapshot ReadSnapshot();
}

public sealed class UnavailableSystemSensorProvider : ISystemSensorProvider
{
    public SensorSnapshot ReadSnapshot() => SensorSnapshot.Empty;
    public void Dispose() { }
}

public sealed record WidgetConfiguration(
    string Id,
    string Type,
    int X,
    int Y,
    int Width,
    int Height,
    bool Enabled = true,
    string Text = "");

public sealed record WidgetPageConfiguration(
    string Name,
    IReadOnlyList<WidgetConfiguration> Widgets);

public sealed record WidgetRenderContext(
    DateTime LocalTime,
    SensorSnapshot Sensors,
    string GameStatus,
    string FreeText = "");

public sealed class OledWidgetRenderer
{
    public OledFrame Render(WidgetPageConfiguration page, WidgetRenderContext context, bool inverted = false)
    {
        var canvas = new OledCanvas();
        foreach (var widget in page.Widgets.Where(widget => widget.Enabled))
        {
            switch (widget.Type.ToLowerInvariant())
            {
                case "clock":
                    canvas.DrawText(context.LocalTime.ToString("HH:mm"), widget.X, widget.Y);
                    break;
                case "cpu":
                    canvas.DrawText($"CPU {Format(context.Sensors.CpuTemperature)}", widget.X, widget.Y);
                    break;
                case "gpu":
                    var gpu = context.Sensors.GpuTemperatures.FirstOrDefault();
                    canvas.DrawText($"GPU {Format(gpu.Temperature)}", widget.X, widget.Y);
                    break;
                case "status":
                    canvas.DrawText(context.GameStatus, widget.X, widget.Y);
                    break;
                case "text":
                    canvas.DrawText(widget.Text.Length > 0 ? widget.Text : context.FreeText, widget.X, widget.Y);
                    break;
                case "bar":
                    canvas.DrawProgressBar(widget.X, widget.Y, widget.Width, widget.Height, context.Sensors.CpuTemperature is null ? 0 : context.Sensors.CpuTemperature.Value / 100);
                    break;
            }
        }
        return canvas.ToFrame(inverted);
    }

    private static string Format(double? value) => value is null ? "N/A" : $"{value:0}C";
}
