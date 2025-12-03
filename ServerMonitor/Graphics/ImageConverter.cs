using SkiaSharp;

namespace ServerMonitor.Graphics;

public static class ImageConverter
{
    // 预分配缓冲区大小：320 * 480 * 2 bytes
    public const int MaxFrameSize = 320 * 480 * 2;

    public static byte[] ConvertToRgb565(SKBitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] resultBuffer = new byte[width * height * 2];

        unsafe
        {
            byte* srcPtr = (byte*)bitmap.GetPixels();

            fixed (byte* dstPtr = resultBuffer)
            {
                int pixelCount = width * height;
                byte* d = dstPtr;
                byte* s = srcPtr;

                for (int i = 0; i < pixelCount; i++)
                {
                    // SkiaSharp 默认内存布局通常为 RGBA 或 BGRA
                    // 这里按顺序读取字节
                    byte r = s[0];
                    byte g = s[1];
                    byte b = s[2];

                    // RGB888 -> RGB565
                    ushort r5 = (ushort)((r >> 3) & 0x1F);
                    ushort g6 = (ushort)((g >> 2) & 0x3F);
                    ushort b5 = (ushort)((b >> 3) & 0x1F);
                    ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);

                    // Little Endian
                    d[0] = (byte)(rgb565 & 0xFF);
                    d[1] = (byte)((rgb565 >> 8) & 0xFF);

                    s += 4; // 跳过 4 字节 (R,G,B,A)
                    d += 2; // 写入 2 字节
                }
            }
        }
        return resultBuffer;
    }
}