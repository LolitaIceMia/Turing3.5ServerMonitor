using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Management;

namespace ServerMonitor.Hardware;

public static class PortFinder
{
    private const string TargetVid = "1A86";
    private const string TargetPid = "5722";

    public static string? FindTuringPort()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return FindPortWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return FindPortLinux();
        return null;
    }

    private static string? FindPortLinux()
    {
        Console.WriteLine("[Debug] 启动新版扫描策略: 遍历 USB 总线...");

        // 策略 A: 遍历 /sys/bus/usb/devices (这是 Linux USB 设备的“户籍处”)
        string usbBusDir = "/sys/bus/usb/devices";
        if (Directory.Exists(usbBusDir))
        {
            foreach (var devDir in Directory.GetDirectories(usbBusDir))
            {
                // 1. 检查 VID 和 PID
                string vid = ReadFile(Path.Combine(devDir, "idVendor"));
                string pid = ReadFile(Path.Combine(devDir, "idProduct"));

                if (string.Equals(vid, TargetVid, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(pid, TargetPid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[Debug] 在 USB 总线找到目标设备: {devDir} (VID:{vid})");

                    // 2. 找到了设备，现在找它下面的 TTY 接口
                    // 结构通常是: 设备目录/接口目录(如1-7:1.0)/tty/ttyACM0

                    // 遍历所有子目录（寻找接口目录）
                    foreach (var subDir in Directory.GetDirectories(devDir))
                    {
                        // 检查子目录下是否有 tty 文件夹
                        string ttyBase = Path.Combine(subDir, "tty");
                        if (Directory.Exists(ttyBase))
                        {
                            // tty 文件夹下通常就是 ttyACM0 文件夹
                            var ttyDirs = Directory.GetDirectories(ttyBase);
                            foreach (var tty in ttyDirs)
                            {
                                string ttyName = Path.GetFileName(tty);
                                if (ttyName.StartsWith("tty"))
                                {
                                    Console.WriteLine($"[Debug] -> 成功关联到 TTY: /dev/{ttyName}");
                                    return $"/dev/{ttyName}";
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("[Debug] 警告: /sys/bus/usb/devices 不存在，无法进行总线扫描。");
        }

        // 策略 B: 最后的保底 (如果上面的高级扫描都失败了)
        Console.WriteLine("[Debug] 自动扫描未匹配，尝试检测常见端口...");
        string[] fallbacks = { "/dev/ttyACM0", "/dev/ttyUSB0", "/dev/ttyACM1" };
        foreach (var path in fallbacks)
        {
            if (File.Exists(path))
            {
                // 这里我们不再检查 VID/PID (因为读取失败了)，直接盲猜连接
                // 只要文件存在，就很有可能是它
                Console.WriteLine($"[Debug] 保底策略: 发现存在 {path}，强制使用。");
                return path;
            }
        }

        Console.WriteLine("[Debug] 彻底失败: 未找到任何可用端口。");
        return null;
    }

    private static string? ReadFile(string path)
    {
        try { if (File.Exists(path)) return File.ReadAllText(path).Trim(); } catch {}
        return null;
    }

    private static string? FindPortWindows()
    {
        try {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");
            foreach (ManagementObject obj in searcher.Get()) {
                string devId = obj["DeviceID"]?.ToString() ?? "";
                if (devId.Contains($"VID_{TargetVid}", StringComparison.OrdinalIgnoreCase) &&
                    devId.Contains($"PID_{TargetPid}", StringComparison.OrdinalIgnoreCase)) {
                    var match = Regex.Match(obj["Caption"]?.ToString() ?? "", @"\((COM\d+)\)");
                    if (match.Success) return match.Groups[1].Value;
                }
            }
        } catch {}
        return null;
    }
}