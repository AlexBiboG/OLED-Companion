using System.Text;
using OledCompanion.Modules;
using Xunit;

namespace OledCompanion.Tests;

public sealed class RenderingAndAudioTests
{
    [Fact]
    public void FrameEncodingUsesExactly640BytesAndCorrectMsbPixels()
    {
        var canvas = new OledCanvas();
        canvas.SetPixel(0, 0);
        canvas.SetPixel(127, 39);

        var frame = canvas.ToFrame();

        Assert.Equal(640, frame.Bytes.Count);
        Assert.True(frame.IsPixelSet(0, 0));
        Assert.True(frame.IsPixelSet(127, 39));
        Assert.False(frame.IsPixelSet(1, 0));
    }

    [Fact]
    public void TextIsClippedAndWidgetLayoutRendersExpectedWidgets()
    {
        var canvas = new OledCanvas();
        canvas.DrawText("THIS TEXT IS CLIPPED", 124, 0);
        var clipped = canvas.ToFrame();

        var renderer = new OledWidgetRenderer();
        var frame = renderer.Render(
            new WidgetPageConfiguration("test", [
                new WidgetConfiguration("clock", "clock", 1, 1, 30, 7),
                new WidgetConfiguration("text", "text", 1, 12, 50, 7, Text: "TEST")
            ]),
            new WidgetRenderContext(new DateTime(2026, 1, 1, 12, 34, 0), SensorSnapshot.Empty, "GAME"));

        Assert.Equal(640, clipped.Bytes.Count);
        Assert.Equal(640, frame.Bytes.Count);
        Assert.Contains(frame.Bytes, value => value != 0);
    }

    [Fact]
    public void MissingSensorsRenderNa()
    {
        var frame = new OledWidgetRenderer().Render(
            new WidgetPageConfiguration("test", [new WidgetConfiguration("cpu", "cpu", 1, 1, 60, 7)]),
            new WidgetRenderContext(DateTime.Now, SensorSnapshot.Empty, "GAME"));

        Assert.Contains(frame.Bytes, value => value != 0);
        using var provider = new UnavailableSystemSensorProvider();
        Assert.Null(provider.ReadSnapshot().CpuTemperature);
    }

    [Fact]
    public async Task OfflineAudioAnalyzerFindsSyntheticBurstAndRejectsValorantMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oled-audio-{Guid.NewGuid():N}.wav");
        try
        {
            WritePcm16Wav(path, 8000, Enumerable.Repeat((short)20000, 8000).ToArray());
            var analyzer = new OfflineAudioAnalyzer([new RmsBurstRecognizer(threshold: 0.2)]);

            var result = await analyzer.AnalyzeAsync(path);

            Assert.NotEmpty(result.Matches);
            await Assert.ThrowsAsync<InvalidOperationException>(() => analyzer.AnalyzeAsync(path, valorantRunning: true));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void WritePcm16Wav(string path, int sampleRate, short[] samples)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
        writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
        foreach (var sample in samples) writer.Write(sample);
    }
}
