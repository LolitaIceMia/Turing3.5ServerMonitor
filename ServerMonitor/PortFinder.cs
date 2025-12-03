using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
// 如果在 Linux 编译报错找不到 Management，可以用预编译指令包裹，或者确保装了 NuGet 包
using System.Management;

namespace TuringSmartScreen;

public static class PortFinder
{
    // 目标设备的硬件 ID
    private const string TargetVid = "1A86";
    private const string TargetPid = "5722";

    public static string? FindTuringPort()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return FindPortWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return FindPortLinux();
        }

        throw new PlatformNotSupportedException("当前仅支持 Windows 和 Linux");
    }

    // --- Linux 实现 (基于 /sys 文件系统) ---
    private static string? FindPortLinux()
    {
        // 遍历 /sys/class/tty/ 下的所有 tty 设备
        string baseDir = "/sys/class/tty";
        if (!Directory.Exists(baseDir)) return null;

        foreach (var ttyDir in Directory.GetDirectories(baseDir))
        {
            string ttyName = Path.GetFileName(ttyDir);

            // 我们只关心 USB 串口，通常是 ttyACM* 或 ttyUSB*
            if (!ttyName.StartsWith("ttyACM") && !ttyName.StartsWith("ttyUSB"))
                continue;

            // 尝试获取 idVendor 和 idProduct
            // 路径结构通常是: /sys/class/tty/ttyACM0/device/../idVendor
            // device 是一个符号链接，指向 USB 接口

            string devicePath = Path.Combine(ttyDir, "device");
            if (!Directory.Exists(devicePath)) continue;

            // 辅助函数：尝试在当前目录或父目录查找 ID 文件
            string? vid = ReadIdFile(devicePath, "idVendor");
            string? pid = ReadIdFile(devicePath, "idProduct");

            if (vid == TargetVid && pid == TargetPid)
            {
                return $"/dev/{ttyName}";
            }
        }

        return null;
    }

    private static string? ReadIdFile(string devicePath, string filename)
    {
        // 策略：先查 device 目录，如果没有，查 device/.. (父目录)
        // 因为 tty 指向的是 Interface，而 VID/PID 通常在 Device (父级) 上

        string p1 = Path.Combine(devicePath, filename);
        if (File.Exists(p1)) return File.ReadAllText(p1).Trim();

        string p2 = Path.Combine(devicePath, "..", filename);
        if (File.Exists(p2)) return File.ReadAllText(p2).Trim();

        // 有些系统可能在更上一层，简单起见只查两层
        return null;
    }

    // --- Windows 实现 (基于 WMI) ---
    private static string? FindPortWindows()
    {
        try
        {
            // 查询所有即插即用实体，筛选包含 VID/PID 的项
            // 注意：需要 System.Management NuGet 包
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                string deviceId = queryObj["DeviceID"]?.ToString() ?? "";
                string caption = queryObj["Caption"]?.ToString() ?? "";
                // deviceId 示例: USB\VID_1A86&PID_5722\5&2B3E1234&0&1

                if (deviceId.Contains($"VID_{TargetVid}") && deviceId.Contains($"PID_{TargetPid}"))
                {
                    // 从 Caption 中提取 COM 口，例如 "USB-SERIAL CH340 (COM3)"
                    var match = Regex.Match(caption, @"\((COM\d+)\)");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Windows 端口扫描失败: {ex.Message}");
        }
        return null;
    }
}