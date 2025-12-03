namespace ServerMonitor.Protocol;

public enum Command : byte
{
    Reset = 101,        // 0x65
    Clear = 102,        // 0x66
    ScreenOff = 108,    // 0x6C
    ScreenOn = 109,     // 0x6D
    SetBrightness = 110,// 0x6E
    DisplayBitmap = 197 // 0xC5
}