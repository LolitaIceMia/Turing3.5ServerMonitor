using System.IO.Ports;
using SkiaSharp;
using ServerMonitor.Protocol;
using ServerMonitor.Graphics;

namespace ServerMonitor.Hardware;

public class ScreenDriver : IDisposable
{
    private SerialPort? _serialPort;
    private const int ChunkSize = 1024;

    public void Connect(string portName)
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            _serialPort.Dispose();
        }

        _serialPort = new SerialPort(portName)
        {
            BaudRate = 115200,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            DtrEnable = false, // 保持 false 避免重启
            RtsEnable = false,
            WriteTimeout = 2000
        };

        _serialPort.Open();
        Thread.Sleep(1000); // 等待握手

        if (!_serialPort.IsOpen) throw new Exception("端口打开失败");

        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();
    }

    public void SendCommand(Command cmd)
    {
        if (_serialPort?.IsOpen != true) return;
        byte[] packet = TuringProtocol.BuildHeader(cmd, 0, 0, 0, 0);
        _serialPort.Write(packet, 0, packet.Length);
    }

    public void SendImage(SKBitmap bitmap)
    {
        if (_serialPort?.IsOpen != true) return;

        // 1. 转换数据
        byte[] imageData = ImageConverter.ConvertToRgb565(bitmap);

        // 2. 发送头
        byte[] header = TuringProtocol.BuildHeader(Command.DisplayBitmap, 0, 0, bitmap.Width, bitmap.Height);
        _serialPort.Write(header, 0, header.Length);

        // 3. 分块发送
        for (int i = 0; i < imageData.Length; i += ChunkSize)
        {
            int count = Math.Min(ChunkSize, imageData.Length - i);
            _serialPort.Write(imageData, i, count);
        }
    }

    public void Dispose()
    {
        if (_serialPort?.IsOpen == true)
        {
            SendCommand(Command.ScreenOff);
            _serialPort.Close();
        }
        _serialPort?.Dispose();
    }
}