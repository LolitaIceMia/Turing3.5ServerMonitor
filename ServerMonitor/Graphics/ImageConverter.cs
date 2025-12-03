using SkiaSharp;

namespace ServerMonitor.Graphics;

public static class ImageConverter
{
    public static byte[] ConvertToRgb565(SKBitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] resultBuffer = new byte[width * height * 2];

        // 获取图片的“跨度”（每一行实际占用的字节数，可能包含填充）
        int rowBytes = bitmap.RowBytes;

        unsafe
        {
            // 获取源数据的首地址
            byte* baseSrcPtr = (byte*)bitmap.GetPixels();

            fixed (byte* dstPtr = resultBuffer)
            {
                byte* d = dstPtr;

                for (int y = 0; y < height; y++)
                {
                    // 计算当前行的起始地址：基地址 + (行号 * 跨度)
                    // 这样即使有填充字节，也能跳过去，直接对齐到下一行开头
                    byte* rowPtr = baseSrcPtr + (y * rowBytes);

                    for (int x = 0; x < width; x++)
                    {
                        // 计算当前像素的偏移：x * 4 (因为是 32位 BGRA)
                        byte* s = rowPtr + (x * 4);

                        // Windows 上 SkiaSharp 默认是 BGRA 格式
                        // s[0]=Blue, s[1]=Green, s[2]=Red, s[3]=Alpha
                        byte b = s[0];
                        byte g = s[1];
                        byte r = s[2];

                        // RGB888 -> RGB565 转换
                        ushort r5 = (ushort)((r >> 3) & 0x1F);
                        ushort g6 = (ushort)((g >> 2) & 0x3F);
                        ushort b5 = (ushort)((b >> 3) & 0x1F);
                        ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);

                        // 写入结果 (小端序: 低字节在前)
                        d[0] = (byte)(rgb565 & 0xFF);
                        d[1] = (byte)((rgb565 >> 8) & 0xFF);

                        d += 2; // 目标指针前进 2 字节
                    }
                }
            }
        }
        return resultBuffer;
    }
}