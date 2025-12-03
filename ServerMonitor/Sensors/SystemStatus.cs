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

    // GPU (Intel Arc A380)
    public double GpuUsage { get; set; }
    public double GpuTemp { get; set; }
    public double VramUsedGB { get; set; }

    // Network
    public double UploadSpeed { get; set; }   // MB/s
    public double DownloadSpeed { get; set; } // MB/s
}