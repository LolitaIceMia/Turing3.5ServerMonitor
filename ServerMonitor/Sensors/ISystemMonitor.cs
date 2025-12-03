namespace ServerMonitor.Sensors;

public interface ISystemMonitor
{
    SystemStatus GetStatus();
}