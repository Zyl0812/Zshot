namespace Starshot.Core.LongCapture;

public static class GrayscaleConvert
{
    public static byte[] BgraToGray(ReadOnlySpan<byte> bgra, int width, int height)
    {
        var gray = new byte[width * height];
        int n = Math.Min(gray.Length, bgra.Length / 4);
        for (int i = 0; i < n; i++)
        {
            int o = i * 4;
            gray[i] = (byte)((bgra[o] * 29 + bgra[o + 1] * 150 + bgra[o + 2] * 77) >> 8);
        }

        return gray;
    }
}
