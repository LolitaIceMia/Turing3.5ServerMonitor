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
        // 关键：必须调用 Accept 来刷新传感器数据
        _computer.Accept(_visitor);

        var status = new SystemStatus();

        foreach (var hardware in _computer.Hardware)
        {
            // --- CPU (i5-12600KF) ---
            if (hardware.HardwareType == HardwareType.Cpu)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Name == "CPU Total")
                    {
                        status.CpuUsage = sensor.Value ?? 0;
                    }
                    // Intel CPU 通常看 "Core Max" 或 "CPU Package"
                    else if (sensor.SensorType == SensorType.Temperature &&
                            (sensor.Name == "CPU Package" || sensor.Name == "Core Max"))
                    {
                        // 优先取 Package 温度，如果已经取到了就不覆盖
                        if (status.CpuTemp == 0 || sensor.Name == "CPU Package")
                            status.CpuTemp = sensor.Value ?? 0;
                    }
                }
            }

            // --- 内存 ---
            else if (hardware.HardwareType == HardwareType.Memory)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory")
                    {
                        status.MemUsagePercent = sensor.Value ?? 0;
                    }
                    else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Used")
                    {
                        status.MemUsedGB = sensor.Value ?? 0;
                    }
                    else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Available")
                    {
                        // 并不是所有版本都直接提供 Total，这里可以通过 Used + Available 反推
                        // 或者直接用 Used / Percent 计算
                        double available = sensor.Value ?? 0;
                        if (status.MemUsedGB > 0)
                        {
                            status.MemTotalGB = status.MemUsedGB + available;
                        }
                    }
                }
            }

            // --- GPU (Intel Arc A380) ---
            // LibreHardwareMonitor 识别 Intel 独显为 GpuIntel
            else if (hardware.HardwareType == HardwareType.GpuIntel ||
                     hardware.HardwareType == HardwareType.GpuNvidia ||
                     hardware.HardwareType == HardwareType.GpuAmd)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Name == "D3D 3D")
                    {
                        // Intel Arc 有时 "GPU Core" 不准，D3D 3D 反而更能反映游戏/渲染负载
                        // 如果有 "GPU Core"，优先用 Core
                        if (status.GpuUsage == 0) status.GpuUsage = sensor.Value ?? 0;
                    }
                    else if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core")
                    {
                        status.GpuUsage = sensor.Value ?? 0;
                    }
                    else if (sensor.SensorType == SensorType.Temperature && sensor.Name == "GPU Core")
                    {
                        status.GpuTemp = sensor.Value ?? 0;
                    }
                    else if (sensor.SensorType == SensorType.SmallData && sensor.Name == "GPU Memory Used")
                    {
                        // SmallData 通常单位是 MB，我们需要转为 GB
                        status.VramUsedGB = (sensor.Value ?? 0) / 1024.0;
                    }
                    // 有些版本 Vram 也是 Data 类型
                    else if (sensor.SensorType == SensorType.Data && sensor.Name == "GPU Memory Used")
                    {
                        status.VramUsedGB = sensor.Value ?? 0;
                    }
                }
            }

            // --- 网络 ---
            else if (hardware.HardwareType == HardwareType.Network)
            {
                foreach (var sensor in hardware.Sensors)
                {

                    if (sensor.Name == "Upload Speed")
                    {
                        status.UploadSpeed += (sensor.Value ?? 0) / 1024.0;
                    }
                    else if (sensor.Name == "Download Speed")
                    {
                        status.DownloadSpeed += (sensor.Value ?? 0) / 1024.0;
                    }
                }
            }
        }

        // 兜底逻辑：如果没有读取到 Memory Total (可能只有 Used 和 Percent)
        if (status.MemTotalGB == 0 && status.MemUsagePercent > 0)
        {
            status.MemTotalGB = status.MemUsedGB / (status.MemUsagePercent / 100.0);
        }

        return status;
    }

    public void Dispose()
    {
        _computer.Close();
    }

    // LibreHardwareMonitor 需要这个 Visitor 类来触发更新
    private class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) {
            computer.Traverse(this);
        }
        public void VisitHardware(IHardware hardware) {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}