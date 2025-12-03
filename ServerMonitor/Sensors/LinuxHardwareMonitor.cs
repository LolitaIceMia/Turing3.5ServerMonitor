using System.Diagnostics;

namespace ServerMonitor.Sensors;

public class LinuxHardwareMonitor : ISystemMonitor
{
    private long _prevIdleTime = 0;
    private long _prevTotalTime = 0;
    private long _prevRxBytes = 0;
    private long _prevTxBytes = 0;
    private long _lastNetCheckTime = 0;

    // 缓存探测到的路径，避免每次都遍历文件系统
    private string? _cpuTempPath;
    private string? _gpuTempPath;
    private string? _gpuVramPath;
    private bool _gpuPathsLocated = false;

    public LinuxHardwareMonitor()
    {
        LocateCpuSensors();
    }

    private void LocateCpuSensors()
    {
        try
        {
            var zones = Directory.GetDirectories("/sys/class/thermal", "thermal_zone*");
            foreach (var zone in zones)
            {
                string typePath = Path.Combine(zone, "type");
                if (File.Exists(typePath) && File.ReadAllText(typePath).Trim() == "x86_pkg_temp")
                {
                    _cpuTempPath = Path.Combine(zone, "temp");
                    Console.WriteLine($"[Debug] 发现 CPU 温度传感器: {_cpuTempPath}");
                    break;
                }
            }
            if (_cpuTempPath == null && zones.Length > 0)
            {
                _cpuTempPath = Path.Combine(zones[0], "temp");
                Console.WriteLine($"[Debug] 使用默认 CPU 温度传感器: {_cpuTempPath}");
            }
        }
        catch { }
    }

    // 动态搜索 GPU 路径 (首次运行时调用)
    private void LocateGpuSensors()
    {
        if (_gpuPathsLocated) return;
        _gpuPathsLocated = true;

        Console.WriteLine("[Debug] 开始搜索 Intel Arc GPU 传感器...");

        string baseDir = "/sys/class/drm";
        if (!Directory.Exists(baseDir)) return;

        // 遍历 card0, card1, card2...
        foreach (var cardDir in Directory.GetDirectories(baseDir, "card*"))
        {
            // 排除掉 cardX-HDMI 这种子接口，只看主卡目录
            if (Path.GetFileName(cardDir).Contains("-")) continue;

            Console.WriteLine($"[Debug] 检查显卡目录: {cardDir}");

            // 1. 搜索显存 (VRAM)
            // 常见路径优先级:
            // - memory/used (i915)
            // - device/mem_info_vram_used (amdgpu/others)
            // - gt/gt0/memory_used (某些新版 intel 驱动)
            string[] vramCandidates = {
                Path.Combine(cardDir, "memory/used"),
                Path.Combine(cardDir, "device/mem_info_vram_used"),
                Path.Combine(cardDir, "gt/gt0/memory_used")
            };

            foreach (var path in vramCandidates)
            {
                if (File.Exists(path))
                {
                    _gpuVramPath = path;
                    Console.WriteLine($"[Debug] -> 找到显存文件: {_gpuVramPath}");
                    break;
                }
            }

            // 2. 搜索温度 (递归找 hwmon)
            // 路径通常是: device/hwmon/hwmonX/temp1_input
            string deviceDir = Path.Combine(cardDir, "device");
            if (Directory.Exists(deviceDir))
            {
                // 递归找 temp1_input
                var tempFiles = Directory.GetFiles(deviceDir, "temp1_input", SearchOption.AllDirectories);
                if (tempFiles.Length > 0)
                {
                    _gpuTempPath = tempFiles[0];
                    Console.WriteLine($"[Debug] -> 找到温度传感器: {_gpuTempPath}");
                }
            }

            // 如果找到了任意一个，就认定这是我们要的主卡 (假设只有一张独显)
            if (_gpuVramPath != null || _gpuTempPath != null)
            {
                Console.WriteLine("[Debug] 锁定该显卡为主要监控对象。");
                break;
            }
        }

        if (_gpuVramPath == null && _gpuTempPath == null)
        {
            Console.WriteLine("[Debug] 警告: 未找到任何 GPU 传感器文件。可能需要更新内核或安装 intel-gpu-tools。");
        }
    }

    public SystemStatus GetStatus()
    {
        // 懒加载 GPU 路径
        LocateGpuSensors();

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

        // --- GPU (动态读取) ---
        if (_gpuVramPath != null && File.Exists(_gpuVramPath))
        {
            if (long.TryParse(File.ReadAllText(_gpuVramPath), out long vramBytes))
            {
                // 同样注意单位换算
                status.VramUsedGB = vramBytes / 1024.0 / 1024.0 / 1024.0;
            }
        }
        else
        {
            // 没找到文件时，给个模拟值或者 0，防止界面很难看
            status.VramUsedGB = 0;
        }

        if (_gpuTempPath != null && File.Exists(_gpuTempPath))
        {
            if (long.TryParse(File.ReadAllText(_gpuTempPath), out long gTemp))
            {
                status.GpuTemp = gTemp / 1000.0;
            }
        }

        // 尝试计算 GPU 利用率 (Arc 显卡很难直接读取利用率，这里尝试读 gt_act_freq_mhz 侧面反映)
        // 真正的利用率通常需要 fdinfo 解析，比较复杂，暂时设为 0 或保留
        status.GpuUsage = 0;

        // --- 网络 ---
        var net = GetNetworkSpeed();
        status.DownloadSpeed = net.down;
        status.UploadSpeed = net.up;

        return status;
    }

    // ... (GetCpuUsage, GetMemInfo, GetNetworkSpeed 方法保持不变，请直接复用之前的代码) ...
    // 为了节省篇幅，这里省略了未修改的辅助方法
    // 请确保类里包含 GetCpuUsage, GetMemInfo, ParseKb, GetNetworkSpeed 这些方法

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
        long currentRx = 0; long currentTx = 0;
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

        double downSpeed = 0; double upSpeed = 0;
        if (_lastNetCheckTime > 0)
        {
            double seconds = (now - _lastNetCheckTime) / (double)Stopwatch.Frequency;
            if (seconds > 0)
            {
                downSpeed = (currentRx - _prevRxBytes) / 1024.0 / seconds;
                upSpeed = (currentTx - _prevTxBytes) / 1024.0 / seconds;
            }
        }
        _prevRxBytes = currentRx; _prevTxBytes = currentTx; _lastNetCheckTime = now;
        return (downSpeed, upSpeed);
    }
}