using LibreHardwareMonitor.Hardware;

namespace ServerMonitor.Sensors;

public class WindowsHardwareMonitor : ISystemMonitor, IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor;

    public WindowsHardwareMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsNetworkEnabled = true,
            IsStorageEnabled = false,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false
        };

        _computer.Open();
        _visitor = new UpdateVisitor();
    }

    public SystemStatus GetStatus()
    {
        _computer.Accept(_visitor);
        var status = new SystemStatus();

        foreach (var hardware in _computer.Hardware)
        {
            // CPU
            if (hardware.HardwareType == HardwareType.Cpu)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Name == "CPU Total")
                        status.CpuUsage = sensor.Value ?? 0;
                    else if (sensor.SensorType == SensorType.Temperature && (sensor.Name == "CPU Package" || sensor.Name == "Core Max"))
                        if (status.CpuTemp == 0 || sensor.Name == "CPU Package") status.CpuTemp = sensor.Value ?? 0;
                }
            }
            // RAM
            else if (hardware.HardwareType == HardwareType.Memory)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory") status.MemUsagePercent = sensor.Value ?? 0;
                    else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Used") status.MemUsedGB = sensor.Value ?? 0;
                    else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Available")
                    {
                        if (status.MemUsedGB > 0) status.MemTotalGB = status.MemUsedGB + (sensor.Value ?? 0);
                    }
                }
            }
            // GPU
            else if (hardware.HardwareType == HardwareType.GpuIntel || hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && (sensor.Name == "D3D 3D" || sensor.Name == "GPU Core"))
                    {
                        if (status.GpuUsage == 0) status.GpuUsage = sensor.Value ?? 0;
                    }
                    else if (sensor.SensorType == SensorType.Temperature && sensor.Name == "GPU Core")
                        status.GpuTemp = sensor.Value ?? 0;
                    // VRAM 读取已移除
                }
            }
            // Network
            else if (hardware.HardwareType == HardwareType.Network)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.Name == "Upload Speed") status.UploadSpeed += (sensor.Value ?? 0) / 1024.0;
                    else if (sensor.Name == "Download Speed") status.DownloadSpeed += (sensor.Value ?? 0) / 1024.0;
                }
            }
        }

        if (status.MemTotalGB == 0 && status.MemUsagePercent > 0)
            status.MemTotalGB = status.MemUsedGB / (status.MemUsagePercent / 100.0);

        return status;
    }

    public void Dispose() => _computer.Close();

    private class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) { computer.Traverse(this); }
        public void VisitHardware(IHardware hardware) { hardware.Update(); foreach (var sub in hardware.SubHardware) sub.Accept(this); }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}