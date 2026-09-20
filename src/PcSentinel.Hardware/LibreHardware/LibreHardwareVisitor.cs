using LibreHardwareMonitor.Hardware;

namespace PcSentinel.Hardware.LibreHardware;

/// <summary>
/// Traverses the LibreHardwareMonitor computer hardware graph and triggers updates.
/// </summary>
public sealed class LibreHardwareVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        try
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }
        catch
        {
            // Gracefully ignore individual hardware interrogation errors (e.g., disconnected sensors)
        }
    }

    public void VisitSensor(ISensor sensor)
    {
        // Sensors are polled when the parent hardware is updated.
    }

    public void VisitParameter(IParameter parameter)
    {
    }
}
