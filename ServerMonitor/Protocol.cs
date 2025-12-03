using System.Buffers.Binary;

namespace ServerMonitor;

public enum Command : byte
{
    Reset = 101,        // 0x65
    Clear = 102,        // 0x66
    ScreenOff = 108,    // 0x6C
    ScreenOn = 109,     // 0x6D
    SetBrightness = 110,// 0x6E
    DisplayBitmap = 197 // 0xC5
}

public static class Protocol
{
    // 还原 Java/Python 报告中的 6 字节头封装逻辑
    public static byte[] BuildHeader(Command cmd, int x, int y, int width, int height)
    {
        int ex = x + width - 1;
        int ey = y + height - 1;

        var buffer = new byte[6];

        // 这里的位操作逻辑严格遵循 lcd_comm_rev_a.py 的逆向结果
        buffer[0] = (byte)(x >> 2);
        buffer[1] = (byte)(((x & 3) << 6) + (y >> 4));
        buffer[2] = (byte)(((y & 15) << 4) + (ex >> 6));
        buffer[3] = (byte)(((ex & 63) << 2) + (ey >> 8));
        buffer[4] = (byte)(ey & 255);
        buffer[5] = (byte)cmd;

        return buffer;
    }
}