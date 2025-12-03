using SkiaSharp;
using ServerMonitor.Sensors;
using System;

namespace ServerMonitor.Graphics;

public class SceneRenderer : IDisposable
{
    private readonly SKSurface _surface;
    private readonly SKPaint _bgPaint;
    private readonly SKPaint _gridPaint;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _labelPaint;
    private readonly SKPaint _cpuLinePaint;
    private readonly SKPaint _gpuLinePaint;
    private readonly SKPaint _barBgPaint;
    private readonly SKPaint _ramFillPaint;
    private readonly SKPaint _gaugeArcPaint;
    private readonly SKPaint _gaugeNeedlePaint;

    // 历史数据管理器
    private readonly HistoryData _cpuLoadHistory = new(80);
    private readonly HistoryData _gpuLoadHistory = new(80);
    private readonly HistoryData _cpuTempHistory = new(80);
    private readonly HistoryData _gpuTempHistory = new(80);

    public int Width { get; }
    public int Height { get; }

    public SceneRenderer(int width = 320, int height = 480)
    {
        Width = width;
        Height = height;
        _surface = SKSurface.Create(new SKImageInfo(width, height));

        // --- 初始化画笔 ---
        _bgPaint = new SKPaint { Color = SKColors.Black };
        _gridPaint = new SKPaint { Color = SKColors.DarkGray.WithAlpha(80), IsAntialias = true, StrokeWidth = 1 };
        _labelPaint = new SKPaint { Color = SKColors.Gray, TextSize = 14, IsAntialias = true };
        _textPaint = new SKPaint { Color = SKColors.White, TextSize = 22, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

        // 折线图画笔 (抗锯齿，描边模式)
        _cpuLinePaint = new SKPaint { Color = SKColors.DeepSkyBlue, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        _gpuLinePaint = new SKPaint { Color = SKColors.OrangeRed, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };

        // RAM条画笔
        _barBgPaint = new SKPaint { Color = SKColors.DarkGray.WithAlpha(60), IsAntialias = true };
        _ramFillPaint = new SKPaint { Color = SKColors.Gold, IsAntialias = true };

        // 仪表盘画笔
        _gaugeArcPaint = new SKPaint { Color = SKColors.Gray.WithAlpha(100), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 8, StrokeCap = SKStrokeCap.Round };
        _gaugeNeedlePaint = new SKPaint { Color = SKColors.Red, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
    }

    public SKBitmap Render(SystemStatus status)
    {
        var canvas = _surface.Canvas;
        canvas.Clear(SKColors.Black); // 清屏

        // --- 更新历史数据 ---
        _cpuLoadHistory.Add(status.CpuUsage);
        _gpuLoadHistory.Add(status.GpuUsage);
        _cpuTempHistory.Add(status.CpuTemp);
        _gpuTempHistory.Add(status.GpuTemp);

        // ================== A. 占用率折线图 (CPU/GPU Load) ==================
        // 区域: (10, 10) -> (310, 130)
        DrawChartHeader(canvas, 10, 25, "LOAD (%)",
            ("CPU", SKColors.DeepSkyBlue, $"{status.CpuUsage:F0}%"),
            ("GPU", SKColors.OrangeRed, $"{status.GpuUsage:F0}%"));

        DrawLineChart(canvas, new SKRect(10, 40, 310, 130), 100,
            (_cpuLoadHistory.ToArray(), _cpuLinePaint),
            (_gpuLoadHistory.ToArray(), _gpuLinePaint));

        // ================== B. 温度折线图 (CPU/GPU Temp) ==================
        // 区域: (10, 145) -> (310, 265)
        // 动态确定Y轴最大值，至少显示到80度
        double maxTemp = Math.Max(80, Math.Max(_cpuTempHistory.Max(), _gpuTempHistory.Max()));
        DrawChartHeader(canvas, 10, 160, "TEMP (°C)",
            ("CPU", SKColors.DeepSkyBlue, $"{status.CpuTemp:F0}°C"),
            ("GPU", SKColors.OrangeRed, $"{status.GpuTemp:F0}°C"));

        DrawLineChart(canvas, new SKRect(10, 175, 310, 265), (float)maxTemp,
            (_cpuTempHistory.ToArray(), _cpuLinePaint),
            (_gpuTempHistory.ToArray(), _gpuLinePaint));

        // ================== C. RAM 横向条形图 ==================
        // 区域: (10, 280) -> (310, 330)
        DrawRamBar(canvas, 10, 280, 300, status);

        // ================== D. 网络仪表盘 (Upload/Download) ==================
        // 区域: (10, 340) -> (310, 470)
        // 使用两个圆心绘制两个仪表
        DrawGauge(canvas, new SKPoint(80, 420), 60, "UPLOAD", status.UploadSpeed, SKColors.SeaGreen);
        DrawGauge(canvas, new SKPoint(240, 420), 60, "DOWNLOAD", status.DownloadSpeed, SKColors.DodgerBlue);

        return SKBitmap.FromImage(_surface.Snapshot());
    }

    // --- 辅助绘图方法 ---

    // 绘制折线图的标题和图例值
    private void DrawChartHeader(SKCanvas c, float x, float y, string title,
        (string label, SKColor color, string value) v1,
        (string label, SKColor color, string value) v2)
    {
        // 标题
        c.DrawText(title, x, y, _labelPaint);

        // 值1 (右对齐)
        var paint = _textPaint.Clone();
        paint.Color = v1.color; paint.TextSize = 18; paint.TextAlign = SKTextAlign.Right;
        c.DrawText($"{v1.label}: {v1.value}", 310, y, paint);

        // 值2 (右对齐，在值1左边)
        paint.Color = v2.color;
        // 简单计算一下偏移量
        float offset = _textPaint.MeasureText($"{v1.label}: {v1.value}") + 20;
        c.DrawText($"{v2.label}: {v2.value}", 310 - offset, y, paint);
    }

    // 通用双折线图绘制
    private void DrawLineChart(SKCanvas c, SKRect rect, float maxY, params (double[] data, SKPaint paint)[] lines)
    {
        // 1. 绘制网格和背景
        c.DrawRect(rect, _barBgPaint); // 半透明背景
        c.DrawLine(rect.Left, rect.Top, rect.Right, rect.Top, _gridPaint); // 顶线
        c.DrawLine(rect.Left, rect.MidY, rect.Right, rect.MidY, _gridPaint); // 中线
        c.DrawLine(rect.Left, rect.Bottom, rect.Right, rect.Bottom, _gridPaint); // 底线

        // 2. 绘制 Y 轴刻度 (左侧)
        var scalePaint = _labelPaint.Clone(); scalePaint.TextSize = 10;
        c.DrawText($"{maxY:F0}", rect.Left - 2, rect.Top + 5, scalePaint);
        c.DrawText("0", rect.Left - 2, rect.Bottom, scalePaint);

        // 3. 绘制折线
        // 使用 ClipRect 确保线条不会画出边界
        c.Save();
        c.ClipRect(rect);

        foreach (var (data, paint) in lines)
        {
            SKPath path = new SKPath();
            float stepX = rect.Width / (data.Length - 1);

            for (int i = 0; i < data.Length; i++)
            {
                float val = (float)data[i];
                // 归一化 Y 坐标 (值越大，Y越小)
                float y = rect.Bottom - (val / maxY) * rect.Height;
                // 钳制在边界内
                y = Math.Clamp(y, rect.Top, rect.Bottom);

                if (i == 0) path.MoveTo(rect.Left, y);
                else path.LineTo(rect.Left + i * stepX, y);
            }
            c.DrawPath(path, paint);
        }
        c.Restore();
    }

    // 绘制 RAM 条
    private void DrawRamBar(SKCanvas c, float x, float y, float width, SystemStatus status)
    {
        // 标题和文本
        c.DrawText("RAM USAGE", x, y, _labelPaint);
        string ramText = $"{status.MemUsedGB:F1}/{status.MemTotalGB:F0} GB ({status.MemUsagePercent:F0}%)";
        _textPaint.TextSize = 16; _textPaint.TextAlign = SKTextAlign.Right;
        c.DrawText(ramText, x + width, y, _textPaint);

        // 进度条
        float barHeight = 20;
        float barY = y + 15;
        SKRect bgRect = new SKRect(x, barY, x + width, barY + barHeight);
        c.DrawRoundRect(bgRect, 4, 4, _barBgPaint);

        float fillWidth = width * (float)(status.MemUsagePercent / 100.0);
        SKRect fillRect = new SKRect(x, barY, x + fillWidth, barY + barHeight);
        c.DrawRoundRect(fillRect, 4, 4, _ramFillPaint);
    }

    // 绘制仪表盘
    private void DrawGauge(SKCanvas c, SKPoint center, float radius, string label, double speedKbps, SKColor color)
    {
        // 1. 标题
        _labelPaint.TextAlign = SKTextAlign.Center;
        c.DrawText(label, center.X, center.Y - radius - 10, _labelPaint);

        // 2. 仪表盘背景弧线 (135度 到 405度)
        SKRect arcRect = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
        _gaugeArcPaint.Color = SKColors.DarkGray.WithAlpha(80);
        c.DrawArc(arcRect, 135, 270, false, _gaugeArcPaint);

        // 3. 计算进度和指针角度
        // 假设最大速度 10MB/s (10240 KB/s) 为满格，可根据需要调整
        float maxSpeed = 10240f;
        float progress = Math.Clamp((float)speedKbps / maxSpeed, 0, 1);
        float angle = 135 + progress * 270;

        // 4. 绘制进度弧线
        _gaugeArcPaint.Color = color.WithAlpha(150);
        c.DrawArc(arcRect, 135, progress * 270, false, _gaugeArcPaint);

        // 5. 绘制指针
        c.Save();
        c.Translate(center.X, center.Y);
        c.RotateDegrees(angle);
        _gaugeNeedlePaint.Color = color;
        // 画一根从中心向外的线
        c.DrawLine(0, 0, radius - 10, 0, _gaugeNeedlePaint);
        c.Restore();

        // 6. 中心圆点和数值
        c.DrawCircle(center, 5, _gaugeNeedlePaint);

        _textPaint.TextSize = 20; _textPaint.TextAlign = SKTextAlign.Center; _textPaint.Color = color;
        c.DrawText(FormatSpeed(speedKbps), center.X, center.Y + 35, _textPaint);
    }

    private string FormatSpeed(double kbps)
    {
        if (kbps >= 1024 * 1024) return $"{kbps / 1024.0 / 1024.0:F1} GB/s";
        if (kbps >= 1024) return $"{kbps / 1024.0:F1} MB/s";
        return $"{kbps:F0} KB/s";
    }

    public void Dispose()
    {
        _surface.Dispose();
        _bgPaint.Dispose(); _gridPaint.Dispose(); _textPaint.Dispose(); _labelPaint.Dispose();
        _cpuLinePaint.Dispose(); _gpuLinePaint.Dispose();
        _barBgPaint.Dispose(); _ramFillPaint.Dispose();
        _gaugeArcPaint.Dispose(); _gaugeNeedlePaint.Dispose();
    }
}