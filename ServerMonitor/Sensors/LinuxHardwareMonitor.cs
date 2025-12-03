using System.Diagnostics;

namespace ServerMonitor.Sensors;

public class LinuxHardwareMonitor : ISystemMonitor
{
    // 状态缓存
    private long _prevIdleTime = 0;
    private long _prevTotalTime = 0;

    // 网络流量缓存
    private long _prevRxBytes = 0;
    private long _prevTxBytes = 0;
    private long _lastNetCheckTime = 0;

    // 硬件路径缓存
    private string? _cpuTempPath;
    private string? _gpuBasePath;

    public LinuxHardwareMonitor()
    {
        LocateSensors();
    }

    private void LocateSensors()
    {
        // 1. 寻找 CPU 温度传感器 (x86_pkg_temp)
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
            // 没找到专用包温度就用 zone0 兜底
            if (_cpuTempPath == null && zones.Length > 0)
                _cpuTempPath = Path.Combine(zones[0], "temp");
        }
        catch { /* ignore */ }

        // 2. 寻找 Intel Arc GPU (通常是 card0，因为你的 12600KF 没有核显)
        // Intel Arc 在 Linux 6.x 内核下通常暴露在 /sys/class/drm/card0/device
        string card0 = "/sys/class/drm/card0";
        if (Directory.Exists(card0))
        {
            _gpuBasePath = card0;
        }
    }

    public SystemStatus GetStatus()
    {
        var status = new SystemStatus();

        // --- CPU 使用率 ---
        status.CpuUsage = GetCpuUsage();

        // --- CPU 温度 ---
        if (_cpuTempPath != null && File.Exists(_cpuTempPath))
        {
            if (long.TryParse(File.ReadAllText(_cpuTempPath), out long tempMilli))
            {
                status.CpuTemp = tempMilli / 1000.0;
            }
        }

        // --- 内存 ---
        var memInfo = GetMemInfo();
        status.MemTotalGB = memInfo.total / 1024.0 / 1024.0;
        status.MemUsedGB = (memInfo.total - memInfo.available) / 1024.0 / 1024.0;
        if (memInfo.total > 0)
            status.MemUsagePercent = (1.0 - (double)memInfo.available / memInfo.total) * 100.0;

        // --- GPU (Intel Arc A380) ---
        if (_gpuBasePath != null)
        {
            // 1. 显存已用 (bytes) -> /sys/class/drm/card0/memory/used (部分内核支持)
            // 或者尝试 /sys/class/drm/card0/device/mem_info_vram_used (i915/xe 驱动差异)
            string vramPath = Path.Combine(_gpuBasePath, "memory/used");
            if (File.Exists(vramPath) && long.TryParse(File.ReadAllText(vramPath), out long vramBytes))
            {
                status.VramUsedGB = vramBytes / 1024.0 / 1024.0 / 1024.0;
            }

            // 2. GPU 温度 (gt_temp_0) -> /sys/class/drm/card0/device/hwmon/hwmonX/temp1_input
            // 需要遍历 hwmon 目录
            string hwmonBase = Path.Combine(_gpuBasePath, "device/hwmon");
            if (Directory.Exists(hwmonBase))
            {
                var hwmons = Directory.GetDirectories(hwmonBase);
                foreach (var hw in hwmons)
                {
                    string tempInput = Path.Combine(hw, "temp1_input"); // 通常是 GPU 核心温度
                    if (File.Exists(tempInput) && long.TryParse(File.ReadAllText(tempInput), out long gTemp))
                    {
                        status.GpuTemp = gTemp / 1000.0;
                        break;
                    }
                }
            }

            // 3. GPU 利用率 (sysfs 并未直接提供标准利用率，通常需要 intel_gpu_top)
            // 但我们可以尝试读取 gpu_busy_percent (某些驱动版本提供)
            // 或者简单返回 0，因为准确获取需要读取 fdinfo
            // 也可以尝试读取 gt/gt0/freq_mhz 至少显示频率
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
        return (total * 1024, available * 1024); // Convert KB to Bytes
    }

    private long ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return long.Parse(parts[1]);
    }

    private (double down, double up) GetNetworkSpeed()
    {
        long currentRx = 0;
        long currentTx = 0;
        long now = Stopwatch.GetTimestamp();

        try
        {
            // 读取所有物理网卡 (排除 lo)
            foreach (var line in File.ReadLines("/proc/net/dev").Skip(2))
            {
                var parts = line.Trim().Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10 || parts[0] == "lo") continue;

                // parts[0]=name, parts[1]=RX_bytes, parts[9]=TX_bytes
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
                downSpeed = (currentRx - _prevRxBytes) / 1024.0 / seconds; // KB/s
                upSpeed = (currentTx - _prevTxBytes) / 1024.0 / seconds;   // KB/s
            }
        }

        _prevRxBytes = currentRx;
        _prevTxBytes = currentTx;
        _lastNetCheckTime = now;

        return (downSpeed, upSpeed);
    }
}