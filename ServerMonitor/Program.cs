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
        Console.WriteLine("正在扫描 Turing Smart Screen 设备...");

        // 1. 自动查找端口
        string? portName = PortFinder.FindTuringPort();

        if (portName == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("错误: 未找到 VID=1A86 PID=5722 的设备！");
            Console.WriteLine("请检查 USB 连接。Linux 用户请确保有权限读取 /sys/class/tty。");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"已发现设备，端口: {portName}");

        using var driver = new ScreenDriver();
        using var renderer = new SceneRenderer(320, 480);

        // 简单工厂模式选择 Monitor
        ISystemMonitor monitor;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            monitor = new LinuxMonitor();
        else
            monitor = new LinuxMonitor(); // 临时 fallback, Windows 可扩展 WindowsMonitor

        try
        {
            driver.Connect(portName);
            Console.WriteLine("连接成功，正在初始化...");

            // 亮屏与清屏
            driver.SendCommand(Command.ScreenOn);
            driver.SendCommand(Command.Clear);

            Console.WriteLine("开始渲染循环 (按 Ctrl+C 退出)...");

            long frameCount = 0;
            while (true)
            {
                // 1. 获取数据
                double cpu = monitor.GetCpuUsage();
                double mem = monitor.GetMemoryUsage();

                // 2. 绘制画面
                using SKBitmap bitmap = renderer.Render(cpu, mem, $"Frame: {frameCount++}");

                // 3. 发送数据
                driver.SendImage(bitmap);

                // 控制帧率 (例如 10 FPS)
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误: {ex.Message}");
        }
    }
}