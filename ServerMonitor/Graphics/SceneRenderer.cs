using SkiaSharp;

namespace ServerMonitor.Graphics;

public class SceneRenderer : IDisposable
{
    private readonly SKSurface _surface;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _bgPaint;

    public int Width { get; }
    public int Height { get; }

    public SceneRenderer(int width = 320, int height = 480)
    {
        Width = width;
        Height = height;
        _surface = SKSurface.Create(new SKImageInfo(width, height));

        _bgPaint = new SKPaint { Color = SKColors.Black };
        _textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 30,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
    }

    // 渲染一帧画面
    public SKBitmap Render(double cpuUsage, double memUsage, string extraText)
    {
        var canvas = _surface.Canvas;

        // 1. 清屏
        canvas.DrawRect(0, 0, Width, Height, _bgPaint);

        // 2. 绘制内容
        canvas.DrawText("Turing Monitor", Width / 2, 60, _textPaint);

        _textPaint.Color = SKColors.Cyan;
        canvas.DrawText($"CPU: {cpuUsage:F1}%", Width / 2, 180, _textPaint);

        _textPaint.Color = SKColors.Green;
        canvas.DrawText($"MEM: {memUsage:F1}%", Width / 2, 240, _textPaint);

        _textPaint.Color = SKColors.Gray;
        _textPaint.TextSize = 20;
        canvas.DrawText(DateTime.Now.ToString("HH:mm:ss"), Width / 2, 400, _textPaint);
        canvas.DrawText(extraText, Width / 2, 430, _textPaint);

        // 恢复画笔设置以便复用
        _textPaint.TextSize = 30;
        _textPaint.Color = SKColors.White;

        return SKBitmap.FromImage(_surface.Snapshot());
    }

    public void Dispose()
    {
        _textPaint.Dispose();
        _bgPaint.Dispose();
        _surface.Dispose();
    }
}