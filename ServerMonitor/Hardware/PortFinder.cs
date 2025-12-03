using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Management; // Windows需要

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
        string baseDir = "/sys/class/tty";
        if (!Directory.Exists(baseDir)) return null;

        foreach (var ttyDir in Directory.GetDirectories(baseDir))
        {
            string ttyName = Path.GetFileName(ttyDir);
            if (!ttyName.StartsWith("ttyACM") && !ttyName.StartsWith("ttyUSB")) continue;

            string devicePath = Path.Combine(ttyDir, "device");
            if (!Directory.Exists(devicePath)) continue;

            string? vid = ReadIdFile(devicePath, "idVendor");
            string? pid = ReadIdFile(devicePath, "idProduct");

            if (vid == TargetVid && pid == TargetPid) return $"/dev/{ttyName}";
        }
        return null;
    }

    private static string? ReadIdFile(string path, string file)
    {
        string p1 = Path.Combine(path, file);
        if (File.Exists(p1)) return File.ReadAllText(p1).Trim();
        string p2 = Path.Combine(path, "..", file);
        if (File.Exists(p2)) return File.ReadAllText(p2).Trim();
        return null;
    }

    private static string? FindPortWindows()
    {
        try {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");
            foreach (ManagementObject obj in searcher.Get()) {
                string devId = obj["DeviceID"]?.ToString() ?? "";
                if (devId.Contains($"VID_{TargetVid}") && devId.Contains($"PID_{TargetPid}")) {
                    var match = Regex.Match(obj["Caption"]?.ToString() ?? "", @"\((COM\d+)\)");
                    if (match.Success) return match.Groups[1].Value;
                }
            }
        } catch {}
        return null;
    }
}