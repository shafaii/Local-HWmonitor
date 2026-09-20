using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PcSentinel.Core.Enums;
using PcSentinel.Core.Models;
using PcSentinel.Core.Services;
using PcSentinel.Hardware.Monitoring;
using PcSentinel.Infrastructure.Persistence;

namespace PcSentinel.Cli;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("====================================================================");
        Console.WriteLine("       PC SENTINEL - HARDWARE TELEMETRY & MONITORING CLI           ");
        Console.WriteLine("====================================================================");
        Console.ResetColor();

        bool isElevated = IsAdministrator();
        if (isElevated)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Elevation] Running with Administrator Privileges. Ring-0 kernel drivers active.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Elevation] Running standard user. Motherboard voltages and fan speeds may be restricted.");
            Console.WriteLine("            For full sensor access, restart PowerShell as Administrator.");
        }
        Console.ResetColor();

        // Prepare local SQLite database
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(localAppData, "PCSentinel");
        Directory.CreateDirectory(appFolder);
        string dbPath = Path.Combine(appFolder, "telemetry.db");

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddConsole();
        });

        var logger = loggerFactory.CreateLogger<HardwareMonitorService>();
        var repository = new SqliteTelemetryRepository(dbPath);
        var trendAnalyzer = new TrendAnalyzer();

        Console.WriteLine($"[Database] Local SQLite storage ready: {dbPath}");
        Console.WriteLine("[Discovery] Initializing LibreHardwareMonitorLib hardware detection...");

        var monitorService = new HardwareMonitorService(logger);

        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        monitorService.TelemetryUpdated += (sender, snapshot) =>
        {
            try
            {
                // Record snapshot to SQLite asynchronously
                _ = repository.StoreSnapshotAsync(snapshot);

                // Print clean summary to console
                RenderSnapshot(snapshot);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing snapshot: {ex.Message}");
            }
        };

        try
        {
            await monitorService.StartMonitoringAsync(cancellationTokenSource.Token);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Status] Telemetry polling active (1 Hz interval).");
            Console.WriteLine("Press 'Q' to exit, 'D' to run diagnostics, 'H' for hardware list.");
            Console.ResetColor();

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine("\nStopping telemetry...");
                        cancellationTokenSource.Cancel();
                        break;
                    }
                    else if (key.Key == ConsoleKey.H)
                    {
                        PrintHardwareList(monitorService);
                    }
                    else if (key.Key == ConsoleKey.D)
                    {
                        var snap = monitorService.GetLatestSnapshot();
                        if (snap != null)
                        {
                            var report = trendAnalyzer.EvaluateHealth(snap);
                            Console.ForegroundColor = report.OverallState switch
                            {
                                HealthState.Normal => ConsoleColor.Green,
                                HealthState.Warning => ConsoleColor.Yellow,
                                _ => ConsoleColor.Red
                            };
                            Console.WriteLine($"\n--- DIAGNOSTIC DIGEST [{report.OverallState}] ---");
                            Console.WriteLine(report.Summary);
                            foreach (var alert in report.Alerts)
                            {
                                Console.WriteLine($" - [{alert.Severity}] {alert.Message}");
                            }
                            Console.ResetColor();
                        }
                    }
                }
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Monitoring error: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            await monitorService.StopMonitoringAsync();
            monitorService.Dispose();
            Console.WriteLine("PC Sentinel CLI closed safely.");
        }
    }

    private static void RenderSnapshot(SensorSnapshot snapshot)
    {
        var cpuSensors = snapshot.Sensors.Where(s => s.HardwareType == HardwareType.Cpu).ToList();
        var gpuSensors = snapshot.Sensors.Where(s => s.HardwareType == HardwareType.GpuNvidia || s.HardwareType == HardwareType.GpuAmd || s.HardwareType == HardwareType.GpuIntel).ToList();
        var memorySensors = snapshot.Sensors.Where(s => s.HardwareType == HardwareType.Memory).ToList();

        var cpuTemp = cpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.SensorName.Contains("Package") || s.SensorName.Contains("Core Max") || s.SensorName.Contains("Tdie")))?
            .FormattedValue ?? "N/A";
        var cpuLoad = cpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.SensorName.Contains("Total"))?
            .FormattedValue ?? "N/A";

        var gpuTemp = gpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.SensorName.Contains("Core") || s.SensorName.Contains("GPU")))?
            .FormattedValue ?? "N/A";
        var gpuLoad = gpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Load && (s.SensorName.Contains("Core") || s.SensorName.Contains("GPU")))?
            .FormattedValue ?? "N/A";

        var ramLoad = memorySensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.SensorName.Contains("Memory"))?
            .FormattedValue ?? "N/A";

        Console.Write($"\r[{snapshot.Timestamp:HH:mm:ss}] CPU: {cpuLoad,6} @ {cpuTemp,6} | GPU: {gpuLoad,6} @ {gpuTemp,6} | RAM: {ramLoad,6} | Total Sensors: {snapshot.Sensors.Count,-3}  ");
    }

    private static void PrintHardwareList(HardwareMonitorService monitorService)
    {
        Console.WriteLine("\n\n--- DETECTED HARDWARE DEVICES ---");
        var items = monitorService.GetHardwareItems();
        foreach (var item in items)
        {
            Console.WriteLine($" • [{item.HardwareType}] {item.Name} ({item.Sensors.Count} sensors)");
        }
        Console.WriteLine("--------------------------------\n");
    }

    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
