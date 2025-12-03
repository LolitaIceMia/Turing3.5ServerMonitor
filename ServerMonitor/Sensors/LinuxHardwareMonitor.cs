using System.Diagnostics;

namespace ServerMonitor.Sensors;

public class LinuxHardwareMonitor : ISystemMonitor
{
    private long _prevIdleTime = 0;
    private long _prevTotalTime = 0;
    private long _prevRxBytes = 0;
    private long _prevTxBytes = 0;
    private long _lastNetCheckTime = 0;
    private string? _cpuTempPath;
    private string? _gpuBasePath;

    public LinuxHardwareMonitor()
    {
        LocateSensors();
    }

    private void LocateSensors()
    {
        // 1. CPU 温度
        try
        {
            var zones = Directory.GetDirectories("/sys/class/thermal", "thermal_zone*");
            foreach (var zone in zones)
            {
                string typePath = Path.Combine(zone, "type");
                if (File.Exists(typePath) && File.ReadAllText(typePath).Trim() == "x86_pkg_temp")
                {
                    _cpuTempPath = Path.Combine(zone, "temp");
                    break;
                }
            }
            if (_cpuTempPath == null && zones.Length > 0) _cpuTempPath = Path.Combine(zones[0], "temp");
        }
        catch { }

        // 2. GPU (Intel Arc)
        string card0 = "/sys/class/drm/card0";
        if (Directory.Exists(card0)) _gpuBasePath = card0;
    }

    public SystemStatus GetStatus()
    {
        var status = new SystemStatus();

        // --- 内存 ---
        var memInfo = GetMemInfo();
        status.MemTotalGB = memInfo.total / 1024.0 / 1024.0 / 1024.0;
        status.MemUsedGB = (memInfo.total - memInfo.available) / 1024.0 / 1024.0 / 1024.0;
        if (memInfo.total > 0)
            status.MemUsagePercent = (1.0 - (double)memInfo.available / memInfo.total) * 100.0;

        // --- CPU ---
        status.CpuUsage = GetCpuUsage();
        if (_cpuTempPath != null && File.Exists(_cpuTempPath))
        {
            if (long.TryParse(File.ReadAllText(_cpuTempPath), out long tempMilli))
                status.CpuTemp = tempMilli / 1000.0;
        }

        // --- GPU (移除显存监控) ---
        if (_gpuBasePath != null)
        {
            // 仅读取温度
            string hwmonBase = Path.Combine(_gpuBasePath, "device/hwmon");
            if (Directory.Exists(hwmonBase))
            {
                foreach (var hw in Directory.GetDirectories(hwmonBase))
                {
                    string tempInput = Path.Combine(hw, "temp1_input");
                    if (File.Exists(tempInput) && long.TryParse(File.ReadAllText(tempInput), out long gTemp))
                    {
                        status.GpuTemp = gTemp / 1000.0;
                        break;
                    }
                }
            }
            // Intel Arc 的负载通常很难通过 sysfs 直接获取准确值，
            // 暂保持为 0 或你需要集成 intel_gpu_top，此处保持基础架构
        }

        // --- 网络 ---
        var net = GetNetworkSpeed();
        status.DownloadSpeed = net.down;
        status.UploadSpeed = net.up;

        return status;
    }

    private double GetCpuUsage()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/stat");
            var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            long idle = long.Parse(parts[4]) + long.Parse(parts[5]);
            long total = 0;
            for (int i = 1; i < parts.Length; i++) total += long.Parse(parts[i]);

            long diffIdle = idle - _prevIdleTime;
            long diffTotal = total - _prevTotalTime;

            _prevIdleTime = idle;
            _prevTotalTime = total;

            return diffTotal == 0 ? 0 : (1.0 - (double)diffIdle / diffTotal) * 100.0;
        }
        catch { return 0; }
    }

    private (long total, long available) GetMemInfo()
    {
        long total = 0, available = 0;
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:")) total = ParseKb(line);
                else if (line.StartsWith("MemAvailable:")) available = ParseKb(line);
                if (total > 0 && available > 0) break;
            }
        }
        catch { }
        return (total * 1024, available * 1024);
    }

    private long ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out long val)) return val;
        return 0;
    }

    private (double down, double up) GetNetworkSpeed()
    {
        long currentRx = 0;
        long currentTx = 0;
        long now = Stopwatch.GetTimestamp();

        try
        {
            foreach (var line in File.ReadLines("/proc/net/dev").Skip(2))
            {
                var parts = line.Trim().Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10 || parts[0] == "lo") continue;
                currentRx += long.Parse(parts[1]);
                currentTx += long.Parse(parts[9]);
            }
        }
        catch { return (0, 0); }

        double downSpeed = 0;
        double upSpeed = 0;

        if (_lastNetCheckTime > 0)
        {
            double seconds = (now - _lastNetCheckTime) / (double)Stopwatch.Frequency;
            if (seconds > 0)
            {
                downSpeed = (currentRx - _prevRxBytes) / 1024.0 / seconds;
                upSpeed = (currentTx - _prevTxBytes) / 1024.0 / seconds;
            }
        }

        _prevRxBytes = currentRx;
        _prevTxBytes = currentTx;
        _lastNetCheckTime = now;

        return (downSpeed, upSpeed);
    }
}