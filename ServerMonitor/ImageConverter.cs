namespace ServerMonitor;
using SkiaSharp;
public static class ImageConverter
{
    // 预分配缓冲区大小：320 * 480 * 2 bytes = 307,200 bytes
    public const int MaxFrameSize = 320 * 480 * 2;

    public static byte[] ConvertToRgb565(SKBitmap bitmap)
    {
        if (bitmap.ColorType != SKColorType.Rgba8888 && bitmap.ColorType != SKColorType.Bgra8888)
        {
            throw new ArgumentException("Bitmap must be 32-bit (RGBA or BGRA)");
        }

        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] resultBuffer = new byte[width * height * 2];

        unsafe
        {
            // 获取源像素的指针
            byte* srcPtr = (byte*)bitmap.GetPixels();

            // 使用 fixed 固定目标缓冲区，避免 GC 移动
            fixed (byte* dstPtr = resultBuffer)
            {
                int pixelCount = width * height;
                byte* d = dstPtr;
                byte* s = srcPtr;

                for (int i = 0; i < pixelCount; i++)
                {
                    byte r = s[0];
                    byte g = s[1];
                    byte b = s[2];

                    // RGB888 -> RGB565 转换逻辑
                    // R: 取高5位, G: 取高6位, B: 取高5位
                    ushort r5 = (ushort)((r >> 3) & 0x1F);
                    ushort g6 = (ushort)((g >> 2) & 0x3F);
                    ushort b5 = (ushort)((b >> 3) & 0x1F);

                    ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);

                    // Little Endian 处理: 低字节在前
                    d[0] = (byte)(rgb565 & 0xFF);      // Low byte
                    d[1] = (byte)((rgb565 >> 8) & 0xFF); // High byte

                    // 移动指针 (源像素4字节，目标像素2字节)
                    s += 4;
                    d += 2;
                }
            }
        }
        return resultBuffer;
    }
}