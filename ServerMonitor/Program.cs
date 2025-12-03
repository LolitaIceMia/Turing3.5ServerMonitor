using SkiaSharp;
using ServerMonitor;
using TuringSmartScreen;

Console.WriteLine("Scanning Turing Smart Screen device...");

// 1. 自动查找端口
string? portName = PortFinder.FindTuringPort();

if (portName == null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Error: Device not found for VID=1A86 PID=5722!");
    Console.WriteLine("Please check the USB connection. Linux users should ensure they have permission to read /sys/class/tty.");
    Console.WriteLine("Use command: sudo usermod -aG dialout $USER");
    Console.ResetColor();
    return;
}

Console.WriteLine($"Found device: {portName}");

using var driver = new ScreenDriver();
try
{
    driver.Connect(portName);
    Console.WriteLine("Initial device...");
    //driver.Reset();


    // 2. 准备画布 (320x480)
    using var surface = SKSurface.Create(new SKImageInfo(320, 480));
    var canvas = surface.Canvas;
    var paint = new SKPaint
    {
        Color = SKColors.White,
        TextSize = 40,
        IsAntialias = true,
        TextAlign = SKTextAlign.Center
    };

    Console.WriteLine("Running ...");

    int frameCount = 0;
    while (true)
    {
        // --- 绘图逻辑 ---
        canvas.Clear(SKColors.Black); // 背景黑

        // 绘制动态文本
        string text = $"Frame: {frameCount++}";
        canvas.DrawText("Turing Screen", 160, 200, paint);
        canvas.DrawText(text, 160, 260, paint);

        // --- 绘制结束，获取位图 ---
        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);

        // --- 发送到屏幕 ---
        // 注意：全屏刷新 300KB 数据在 USB FS 上较慢，建议优化为局部刷新
        driver.SendImage(0, 0, bitmap);

        // 控制帧率，避免占用过多 CPU
        Thread.Sleep(100);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}