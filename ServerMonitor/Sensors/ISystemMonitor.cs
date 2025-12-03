namespace ServerMonitor.Sensors;

public interface ISystemMonitor
{
    double GetCpuUsage();
    double GetMemoryUsage();
}