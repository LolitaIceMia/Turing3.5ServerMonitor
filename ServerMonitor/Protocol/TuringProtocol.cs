namespace ServerMonitor.Protocol;

public static class TuringProtocol
{
    // 封装 6 字节协议头
    public static byte[] BuildHeader(Command cmd, int x, int y, int width, int height)
    {
        int ex = x + width - 1;
        int ey = y + height - 1;
        var buffer = new byte[6];

        buffer[0] = (byte)(x >> 2);
        buffer[1] = (byte)(((x & 3) << 6) + (y >> 4));
        buffer[2] = (byte)(((y & 15) << 4) + (ex >> 6));
        buffer[3] = (byte)(((ex & 63) << 2) + (ey >> 8));
        buffer[4] = (byte)(ey & 255);
        buffer[5] = (byte)cmd;

        return buffer;
    }
}