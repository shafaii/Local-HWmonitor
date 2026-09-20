using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PcSentinel.Core.Interfaces;
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
            Console.WriteLine("            For full sensor access, restart PowerShell or VS Code as Administrator.");
        }
        Console.ResetColor();

        // Prepare local SQLite database
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(localAppData, "PcSentinel");
        Directory.CreateDirectory(appFolder);
        string dbPath = Path.Combine(appFolder, "telemetry.db");

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddConsole();
        });

        var logger = loggerFactory.CreateLogger<HardwareMonitorService>();
        var repository = new SqliteTelemetryRepository(dbPath);

        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        try
        {
            await repository.InitializeAsync(cancellationTokenSource.Token);
            Console.WriteLine($"[Database] Local SQLite storage ready: {dbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database] Running in-memory mode: {ex.Message}");
        }

        var writer = new ChannelTelemetryWriter(repository);
        var trendAnalyzer = new TrendAnalyzer();

        Console.WriteLine("[Discovery] Initializing LibreHardwareMonitorLib hardware detection...");

        var monitorService = new HardwareMonitorService(
            options: new HardwareMonitorOptions { DefaultPollingInterval = TimeSpan.FromSeconds(1) },
            telemetryWriter: writer,
            trendAnalyzer: trendAnalyzer,
            logger: logger);

        monitorService.TelemetryUpdated += (sender, snapshot) =>
        {
            try
            {
                RenderSnapshot(snapshot);
            }
            catch
            {
                // Ignore transient format exceptions during quick screen refreshes
            }
        };

        try
        {
            await monitorService.StartAsync(cancellationTokenSource.Token);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Status] Telemetry polling active (1000ms interval).");
            Console.WriteLine("Controls: Press 'H' for Hardware List, 'D' for Diagnostics, 'Q' to Quit.");
            Console.ResetColor();
            Console.WriteLine();

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine("\n\nStopping telemetry...");
                        cancellationTokenSource.Cancel();
                        break;
                    }
                    else if (key.Key == ConsoleKey.H)
                    {
                        PrintHardwareList(monitorService.CurrentSnapshot);
                    }
                    else if (key.Key == ConsoleKey.D)
                    {
                        PrintDiagnosticDigest(monitorService.CurrentSnapshot);
                    }
                }
                await Task.Delay(100, cancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Clean exit
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Error] Monitoring error: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            await monitorService.StopAsync();
            monitorService.Dispose();
            await writer.DisposeAsync();
            repository.Dispose();
            Console.WriteLine("\nPC Sentinel CLI closed safely.");
        }
    }

    private static void RenderSnapshot(HardwareSnapshot snapshot)
    {
        var cpuSensors = snapshot.Cpu?.Sensors ?? new System.Collections.Generic.List<Sensor>();
        var gpuSensors = snapshot.Gpu?.Sensors ?? new System.Collections.Generic.List<Sensor>();
        var memorySensors = snapshot.Memory?.Sensors ?? new System.Collections.Generic.List<Sensor>();

        var cpuTemp = cpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.SensorName.Contains("Package", StringComparison.OrdinalIgnoreCase) || s.SensorName.Contains("Core Max", StringComparison.OrdinalIgnoreCase) || s.SensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase)))?
            .FormattedValue ?? cpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.FormattedValue ?? "—";

        var cpuLoad = cpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.SensorName.Contains("Total", StringComparison.OrdinalIgnoreCase))?
            .FormattedValue ?? cpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Load)?.FormattedValue ?? "—";

        var gpuTemp = gpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase) || s.SensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase)))?
            .FormattedValue ?? "—";

        var gpuLoad = gpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Load && (s.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase) || s.SensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase)))?
            .FormattedValue ?? "—";

        var ramLoad = memorySensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.SensorName.Contains("Memory", StringComparison.OrdinalIgnoreCase))?
            .FormattedValue ?? memorySensors.FirstOrDefault(s => s.SensorType == SensorType.Load)?.FormattedValue ?? "—";

        int totalSensors = snapshot.AllSensors.Count();
        Console.Write($"\r[{snapshot.Timestamp:HH:mm:ss}] CPU: {cpuLoad,6} @ {cpuTemp,6} | GPU: {gpuLoad,6} @ {gpuTemp,6} | RAM: {ramLoad,6} | Sensors: {totalSensors,-3}  ");
    }

    private static void PrintHardwareList(HardwareSnapshot? snapshot)
    {
        Console.WriteLine("\n\n==================== DETECTED HARDWARE DEVICES ====================");
        if (snapshot != null && snapshot.Hardware.Count > 0)
        {
            foreach (var item in snapshot.Hardware)
            {
                Console.WriteLine($" • [{item.Type,-11}] {item.Name} ({item.Sensors.Count} sensors)");
                foreach (var sensor in item.Sensors.Take(4))
                {
                    Console.WriteLine($"     - {sensor.SensorName,-24} : {sensor.FormattedValue}");
                }
                if (item.Sensors.Count > 4)
                {
                    Console.WriteLine($"     ... + {item.Sensors.Count - 4} more sensors");
                }
            }
        }
        else
        {
            Console.WriteLine(" (Waiting for first hardware snapshot...)");
        }
        Console.WriteLine("===================================================================\n");
    }

    private static void PrintDiagnosticDigest(HardwareSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            Console.WriteLine("\n[Diagnostics] Telemetry snapshot not ready yet.");
            return;
        }

        var health = SystemHealthState.Evaluate(snapshot);
        Console.ForegroundColor = health.OverallSeverity switch
        {
            HealthSeverity.Normal => ConsoleColor.Green,
            HealthSeverity.Elevated => ConsoleColor.Cyan,
            HealthSeverity.Warning => ConsoleColor.Yellow,
            _ => ConsoleColor.Red
        };

        Console.WriteLine($"\n\n================ DIAGNOSTIC HEALTH CHECK [{health.OverallSeverity}] ================");
        Console.WriteLine($" Machine: {snapshot.MachineName} | OS: {snapshot.OsVersion}");
        Console.WriteLine($" Elevation: {(snapshot.IsElevated ? "Administrator (Full Ring-0 Access)" : "Standard User")}");
        Console.WriteLine($" CPU Load: {health.CpuTotalLoad?.ToString("0.#") ?? "—"}% | CPU Max Temp: {health.CpuMaxTemperature?.ToString("0.#") ?? "—"}°C");
        Console.WriteLine($" GPU Max Temp: {health.GpuMaxTemperature?.ToString("0.#") ?? "—"}°C | RAM Utilization: {health.MemoryUtilization?.ToString("0.#") ?? "—"}%");
        Console.WriteLine("-------------------------------------------------------------------");

        if (health.HealthNotices.Count > 0)
        {
            Console.WriteLine(" Notices:");
            foreach (var notice in health.HealthNotices)
            {
                Console.WriteLine($"   ! {notice}");
            }
        }
        else
        {
            Console.WriteLine(" Status: All monitored sensors operating within nominal safe limits.");
        }
        Console.WriteLine("===================================================================\n");
        Console.ResetColor();
    }

    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
