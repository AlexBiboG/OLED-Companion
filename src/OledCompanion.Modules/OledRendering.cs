namespace OledCompanion.Modules;

public sealed class OledFrame
{
    public const int Width = 128;
    public const int Height = 40;
    public const int ByteCount = Width * Height / 8;

    private readonly byte[] _bytes;

    public OledFrame(IEnumerable<byte> bytes)
    {
        _bytes = bytes.ToArray();
        if (_bytes.Length != ByteCount)
            throw new ArgumentException($"A {Width}x{Height} frame must contain exactly {ByteCount} bytes.", nameof(bytes));
    }

    public IReadOnlyList<byte> Bytes => _bytes;

    public bool IsPixelSet(int x, int y)
    {
        if (x is < 0 or >= Width || y is < 0 or >= Height)
            return false;
        var index = y * (Width / 8) + x / 8;
        return (_bytes[index] & (1 << (7 - x % 8))) != 0;
    }
}

public sealed class OledCanvas
{
    private readonly bool[,] _pixels = new bool[OledFrame.Height, OledFrame.Width];

    public void Clear(bool value = false)
    {
        Array.Clear(_pixels);
        if (value)
            for (var y = 0; y < OledFrame.Height; y++)
                for (var x = 0; x < OledFrame.Width; x++)
                    _pixels[y, x] = true;
    }

    public void SetPixel(int x, int y, bool value = true)
    {
        if (x is >= 0 and < OledFrame.Width && y is >= 0 and < OledFrame.Height)
            _pixels[y, x] = value;
    }

    public void DrawLine(int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            SetPixel(x0, y0);
            if (x0 == x1 && y0 == y1) return;
            var twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    public void DrawRectangle(int x, int y, int width, int height, bool filled = false)
    {
        if (filled)
            for (var row = y; row < y + height; row++)
                for (var column = x; column < x + width; column++) SetPixel(column, row);
        else
        {
            DrawLine(x, y, x + width - 1, y);
            DrawLine(x, y + height - 1, x + width - 1, y + height - 1);
            DrawLine(x, y, x, y + height - 1);
            DrawLine(x + width - 1, y, x + width - 1, y + height - 1);
        }
    }

    public void DrawProgressBar(int x, int y, int width, int height, double value)
    {
        DrawRectangle(x, y, width, height);
        var fill = (int)Math.Round(Math.Clamp(value, 0, 1) * Math.Max(0, width - 2));
        if (fill > 0) DrawRectangle(x + 1, y + 1, fill, Math.Max(0, height - 2), true);
    }

    public void DrawText(string text, int x, int y, bool inverted = false)
    {
        var cursor = x;
        foreach (var character in text ?? string.Empty)
        {
            if (cursor >= OledFrame.Width) break;
            var glyph = Glyphs.Get(character);
            for (var row = 0; row < 7; row++)
                for (var column = 0; column < 5; column++)
                    if (((glyph[row] >> (4 - column)) & 1) != 0)
                        SetPixel(cursor + column, y + row, !inverted);
            cursor += 6;
        }
    }

    public OledFrame ToFrame(bool inverted = false)
    {
        var bytes = new byte[OledFrame.ByteCount];
        for (var y = 0; y < OledFrame.Height; y++)
            for (var x = 0; x < OledFrame.Width; x++)
                if (_pixels[y, x] ^ inverted)
                    bytes[y * 16 + x / 8] |= (byte)(1 << (7 - x % 8));
        return new OledFrame(bytes);
    }

    private static class Glyphs
    {
        private static readonly byte[] Blank = [0, 0, 0, 0, 0, 0, 0];
        private static readonly IReadOnlyDictionary<char, byte[]> Map = new Dictionary<char, byte[]>
        {
            [' '] = Blank, [':'] = [0, 0, 4, 0, 0, 4, 0], ['-'] = [0, 0, 0, 31, 0, 0, 0],
            ['0'] = [14, 17, 19, 21, 25, 17, 14], ['1'] = [4, 12, 4, 4, 4, 4, 14],
            ['2'] = [14, 17, 1, 2, 4, 8, 31], ['3'] = [30, 1, 1, 14, 1, 1, 30],
            ['4'] = [2, 6, 10, 18, 31, 2, 2], ['5'] = [31, 16, 16, 30, 1, 1, 30],
            ['6'] = [14, 16, 16, 30, 17, 17, 14], ['7'] = [31, 1, 2, 4, 8, 8, 8],
            ['8'] = [14, 17, 17, 14, 17, 17, 14], ['9'] = [14, 17, 17, 15, 1, 1, 14],
            ['A'] = [14, 17, 17, 31, 17, 17, 17], ['C'] = [14, 17, 16, 16, 16, 17, 14],
            ['G'] = [14, 17, 16, 23, 17, 17, 14], ['I'] = [31, 4, 4, 4, 4, 4, 31],
            ['L'] = [16, 16, 16, 16, 16, 16, 31], ['N'] = [17, 25, 21, 19, 17, 17, 17],
            ['O'] = [14, 17, 17, 17, 17, 17, 14], ['P'] = [30, 17, 17, 30, 16, 16, 16],
            ['R'] = [30, 17, 17, 30, 20, 18, 17], ['T'] = [31, 4, 4, 4, 4, 4, 4],
            ['U'] = [17, 17, 17, 17, 17, 17, 14], ['V'] = [17, 17, 17, 17, 17, 10, 4],
            ['Y'] = [17, 17, 10, 4, 4, 4, 4]
        };

        public static byte[] Get(char character) =>
            Map.TryGetValue(char.ToUpperInvariant(character), out var glyph) ? glyph : Blank;
    }
}
