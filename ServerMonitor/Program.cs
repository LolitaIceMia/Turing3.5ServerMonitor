using System.Runtime.InteropServices;
using SkiaSharp;
using ServerMonitor.Hardware;
using ServerMonitor.Graphics;
using ServerMonitor.Protocol;
using ServerMonitor.Sensors;

namespace ServerMonitor;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Turing Smart Screen Monitor ===");

        // 1. 初始化传感器
        ISystemMonitor monitor;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("OS: Linux (Loading LinuxHardwareMonitor...)");
            monitor = new LinuxHardwareMonitor();
        }
        else
        {
            Console.WriteLine("OS: Windows (Loading MockMonitor...)");
            monitor = new WindowsHardwareMonitor();
        }

        // 2. 查找屏幕
        string? portName = PortFinder.FindTuringPort();
        if (portName == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("错误: 未找到设备! (VID=1A86 PID=5722)");
            Console.ResetColor();
            return;
        }
        Console.WriteLine($"设备已连接: {portName}");

        // 3. 启动循环
        using var driver = new ScreenDriver();
        using var renderer = new SceneRenderer(320, 480);

        try
        {
            driver.Connect(portName);
            driver.SendCommand(Command.ScreenOn);
            driver.SendCommand(Command.Clear);

            Console.WriteLine("开始监控 (按 Ctrl+C 退出)...");

            while (true)
            {
                // 获取数据
                var status = monitor.GetStatus();

                // 渲染
                using SKBitmap frame = renderer.Render(status);

                // 发送
                driver.SendImage(frame);

                // Linux下建议 0.5秒 - 1秒刷新一次，太快会占满 USB 带宽
                Thread.Sleep(500);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"运行时错误: {ex.Message}");
        }
    }
}