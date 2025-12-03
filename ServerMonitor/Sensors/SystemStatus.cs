namespace ServerMonitor.Sensors;

public struct SystemStatus
{
    // CPU
    public double CpuUsage { get; set; }
    public double CpuTemp { get; set; } // 摄氏度

    // Memory
    public double MemUsagePercent { get; set; }
    public double MemTotalGB { get; set; }
    public double MemUsedGB { get; set; }

    // GPU
    public double GpuUsage { get; set; }
    public double GpuTemp { get; set; }

    // Network
    public double UploadSpeed { get; set; }   // KB/s
    public double DownloadSpeed { get; set; } // KB/s
}