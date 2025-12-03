namespace ServerMonitor.Sensors;

public class WindowsHardwareMonitor : ISystemMonitor
{
    // todo 简单的模拟实现，真实环境建议使用 LibreHardwareMonitor
    public SystemStatus GetStatus()
    {
        return new SystemStatus
        {
            CpuUsage = 15.5,
            CpuTemp = 45.0,
            MemTotalGB = 32.0,
            MemUsedGB = 8.5,
            MemUsagePercent = 26.5,
            GpuTemp = 40.0,
            UploadSpeed = 120.5,
            DownloadSpeed = 5020.0
        };
    }
}