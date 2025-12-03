namespace ServerMonitor.Sensors;

public class LinuxMonitor : ISystemMonitor
{
    private long _prevIdleTime = 0;
    private long _prevTotalTime = 0;

    public double GetCpuUsage()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/stat");
            var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // user, nice, system, idle, iowait, irq, softirq, steal
            long idle = long.Parse(parts[4]) + long.Parse(parts[5]); // idle + iowait
            long total = 0;
            for (int i = 1; i < parts.Length; i++) total += long.Parse(parts[i]);

            long diffIdle = idle - _prevIdleTime;
            long diffTotal = total - _prevTotalTime;

            _prevIdleTime = idle;
            _prevTotalTime = total;

            if (diffTotal == 0) return 0;
            return (1.0 - (double)diffIdle / diffTotal) * 100.0;
        }
        catch { return 0; }
    }

    public double GetMemoryUsage()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/meminfo");
            long total = 0, available = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                    total = ParseMemValue(line);
                if (line.StartsWith("MemAvailable:"))
                    available = ParseMemValue(line);
            }

            if (total == 0) return 0;
            return ((double)(total - available) / total) * 100.0;
        }
        catch { return 0; }
    }

    private long ParseMemValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return long.Parse(parts[1]); // KB
    }
}