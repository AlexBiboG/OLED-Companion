namespace OledCompanion.Modules;

public sealed record AudioPatternMatch(
    string Pattern,
    TimeSpan Timestamp,
    double Confidence);

public sealed record AudioAnalysisResult(
    string FilePath,
    TimeSpan Duration,
    IReadOnlyList<AudioPatternMatch> Matches);

public interface IAudioPatternRecognizer
{
    string PatternName { get; }
    IEnumerable<AudioPatternMatch> Recognize(ReadOnlyMemory<float> samples, int sampleRate, TimeSpan offset);
}

/// <summary>Small offline prototype recognizer for a defined RMS burst pattern.</summary>
public sealed class RmsBurstRecognizer(
    string patternName = "RMS-BURST",
    double threshold = 0.25) : IAudioPatternRecognizer
{
    public string PatternName { get; } = patternName;

    public IEnumerable<AudioPatternMatch> Recognize(ReadOnlyMemory<float> samples, int sampleRate, TimeSpan offset)
    {
        if (samples.IsEmpty) yield break;
        var rms = Math.Sqrt(samples.Span.ToArray().Average(sample => sample * sample));
        if (rms >= threshold)
            yield return new AudioPatternMatch(PatternName, offset, Math.Clamp(rms / Math.Max(threshold, 0.001), 0, 1));
    }
}

/// <summary>
/// An offline-only WAV analyzer. It deliberately has no microphone, WASAPI or process integration.
/// </summary>
public sealed class OfflineAudioAnalyzer(IEnumerable<IAudioPatternRecognizer> recognizers)
{
    private readonly IReadOnlyList<IAudioPatternRecognizer> _recognizers = recognizers.ToArray();

    public async Task<AudioAnalysisResult> AnalyzeAsync(
        string localWavPath,
        bool valorantRunning = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWavPath);
        if (valorantRunning)
            throw new InvalidOperationException("Offline-Audioanalyse ist während Valorant bewusst deaktiviert.");

        await using var stream = File.OpenRead(localWavPath);
        var (samples, sampleRate) = await ReadPcm16WavAsync(stream, cancellationToken);
        var matches = new List<AudioPatternMatch>();
        const int windowSize = 2048;
        for (var offset = 0; offset < samples.Length; offset += windowSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(windowSize, samples.Length - offset);
            foreach (var recognizer in _recognizers)
                matches.AddRange(recognizer.Recognize(samples.AsMemory(offset, length), sampleRate, TimeSpan.FromSeconds((double)offset / sampleRate)));
        }

        return new AudioAnalysisResult(
            localWavPath,
            TimeSpan.FromSeconds((double)samples.Length / sampleRate),
            matches);
    }

    private static Task<(float[] Samples, int SampleRate)> ReadPcm16WavAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("WAV RIFF header missing.");
        reader.ReadInt32();
        if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("WAV WAVE header missing.");
        short channels = 0, bits = 0;
        int sampleRate = 0;
        byte[]? data = null;
        while (stream.Position + 8 <= stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = new string(reader.ReadChars(4));
            var size = reader.ReadInt32();
            if (size < 0 || stream.Position + size > stream.Length) throw new InvalidDataException("Invalid WAV chunk.");
            if (id == "fmt ")
            {
                if (reader.ReadInt16() != 1) throw new InvalidDataException("Only PCM WAV is supported.");
                channels = reader.ReadInt16(); sampleRate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16();
                stream.Position += size - 16;
            }
            else if (id == "data") data = reader.ReadBytes(size);
            else stream.Position += size;
            if ((size & 1) != 0) stream.Position++;
        }
        if (channels <= 0 || sampleRate <= 0 || bits != 16 || data is null)
            throw new InvalidDataException("Only PCM16 WAV files with audio data are supported.");

        var sampleCount = data.Length / 2 / channels;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var sum = 0d;
            for (var channel = 0; channel < channels; channel++)
            {
                var index = (i * channels + channel) * 2;
                sum += BitConverter.ToInt16(data, index) / 32768d;
            }
            samples[i] = (float)(sum / channels);
        }
        return Task.FromResult((samples, sampleRate));
    }
}
