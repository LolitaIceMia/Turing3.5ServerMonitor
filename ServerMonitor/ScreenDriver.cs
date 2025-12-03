using System.IO.Ports;
using SkiaSharp;

namespace ServerMonitor;

public class ScreenDriver : IDisposable
{
    private SerialPort _serialPort;
    private const int ChunkSize = 1024; // 安全的分块大小

    public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

    public void Connect(string portName)
    {
        // 如果旧连接还开着，先关掉
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
        }

        _serialPort = new SerialPort(portName)
        {
            BaudRate = 115200,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            DtrEnable = false,
            RtsEnable = false
        };

        _serialPort.Open();
        Thread.Sleep(1000);

        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();
    }

    public void Reset()
    {
        SendCommand(Command.Reset);
        Thread.Sleep(100); // 必要的硬件复位等待
        SendCommand(Command.ScreenOn);
        SendCommand(Command.Clear);
    }

    public void SendImage(int x, int y, SKBitmap bitmap)
    {
        if (!_serialPort.IsOpen) return;

        // 1. 生成 RGB565 数据
        byte[] imageData = ImageConverter.ConvertToRgb565(bitmap);

        // 2. 生成协议头
        byte[] header = Protocol.BuildHeader(Command.DisplayBitmap, x, y, bitmap.Width, bitmap.Height);

        // 3. 发送头
        _serialPort.Write(header, 0, header.Length);

        // 4. 分块发送图片数据
        for (int i = 0; i < imageData.Length; i += ChunkSize)
        {
            int remaining = imageData.Length - i;
            int count = Math.Min(remaining, ChunkSize);
            _serialPort.Write(imageData, i, count);

            // 虽然 CDC 有流控，但为了保险，微小的延时可以防止丢包
            // 视具体机器性能而定，如果画面撕裂，可取消注释
            // Thread.Sleep(1);
        }
    }

    private void SendCommand(Command cmd, byte param = 0)
    {
        // 简单指令通常复用 Header 结构，坐标设为 0
        // 部分指令（如亮度）可能需要特殊处理，这里以通用指令为例
        // 对于 Reset/Clear，坐标参数通常会被忽略
        byte[] packet = Protocol.BuildHeader(cmd, 0, 0, 0, 0);
        _serialPort.Write(packet, 0, packet.Length);
    }

    public void Dispose()
    {
        if (_serialPort?.IsOpen == true)
        {
            SendCommand(Command.ScreenOff); // 退出前关屏
            _serialPort.Close();
        }
        _serialPort?.Dispose();
    }
}