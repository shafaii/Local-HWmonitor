import React, { useState, useEffect, useMemo } from 'react';
import {
  Cpu,
  HardDrive,
  Activity,
  Layers,
  Clock,
  AlertTriangle,
  Settings as SettingsIcon,
  ShieldCheck,
  RefreshCw,
  Trash2,
  Database,
  Search,
  CheckCircle2,
  Sliders,
  Info,
  Monitor,
  Terminal,
  ExternalLink,
  ChevronRight,
  AlertCircle
} from 'lucide-react';

type NavigationItem = 'dashboard' | 'hardware' | 'history' | 'alerts' | 'settings' | 'native-guide';
type TimeRange = '5m' | '15m' | '1h' | '6h' | '24h' | '7d';

interface HistoricalPoint {
  time: string;
  min: number;
  max: number;
  avg: number;
  count: number;
}

interface Observation {
  id: string;
  type: string;
  severity: 'Warning' | 'Critical';
  hardware: string;
  sensor: string;
  message: string;
  timestamp: string;
}

export default function App() {
  const [activeTab, setActiveTab] = useState<NavigationItem>('dashboard');
  const [pollingRateMs, setPollingRateMs] = useState<number>(1000);
  const [retentionDays, setRetentionDays] = useState<number>(7);
  const [totalReadings, setTotalReadings] = useState<number>(142980);
  const [dbSizeBytes, setDbSizeBytes] = useState<number>(4820000);
  const [statusMessage, setStatusMessage] = useState<string>('Telemetry persistence active (WAL mode enabled)');

  // Configurable Demo Profile (to demonstrate UI with different configurations)
  const [cpuModel, setCpuModel] = useState<string>('AMD Ryzen 7 7800X3D');
  const [gpuModel, setGpuModel] = useState<string>('NVIDIA GeForce RTX 4070');
  const [ramSpec, setRamSpec] = useState<string>('32 GB DDR5 6000');
  const [storageModel, setStorageModel] = useState<string>('Crucial T700 2TB NVMe');

  // Selected sensor for History tab
  const [selectedDevice, setSelectedDevice] = useState<string>('cpu');
  const [selectedSensor, setSelectedSensor] = useState<string>('Core Max Temp');
  const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRange>('15m');

  // Search in Hardware tab
  const [hardwareSearch, setHardwareSearch] = useState<string>('');

  // Live telemetry data stream (30-sample sliding windows for sparklines)
  const [cpuLoadHistory, setCpuLoadHistory] = useState<number[]>(() =>
    Array.from({ length: 30 }, () => Math.floor(18 + Math.random() * 15))
  );
  const [cpuTempHistory, setCpuTempHistory] = useState<number[]>(() =>
    Array.from({ length: 30 }, () => Math.floor(46 + Math.random() * 8))
  );
  const [gpuLoadHistory, setGpuLoadHistory] = useState<number[]>(() =>
    Array.from({ length: 30 }, () => Math.floor(25 + Math.random() * 20))
  );
  const [gpuTempHistory, setGpuTempHistory] = useState<number[]>(() =>
    Array.from({ length: 30 }, () => Math.floor(52 + Math.random() * 5))
  );

  // Alerts stream
  const [observations, setObservations] = useState<Observation[]>([
    {
      id: 'obs-1',
      type: 'RapidThermalEscalation',
      severity: 'Warning',
      hardware: cpuModel,
      sensor: 'Package Temp',
      message: 'Rapid thermal escalation detected: +4.8°C/s (Current: 76.2°C)',
      timestamp: '14:28:12'
    },
    {
      id: 'obs-2',
      type: 'SustainedHighUtilization',
      severity: 'Warning',
      hardware: cpuModel,
      sensor: 'CPU Total Load',
      message: 'Sustained high utilization above 90% for 22s (Current: 92.4%)',
      timestamp: '14:25:04'
    }
  ]);

  // Live telemetry interval simulator
  useEffect(() => {
    const timer = setInterval(() => {
      setCpuLoadHistory(prev => {
        const nextVal = Math.min(100, Math.max(5, prev[prev.length - 1] + (Math.random() * 12 - 6)));
        return [...prev.slice(1), Math.round(nextVal)];
      });

      setCpuTempHistory(prev => {
        const nextVal = Math.min(95, Math.max(35, prev[prev.length - 1] + (Math.random() * 3 - 1.4)));
        return [...prev.slice(1), Math.round(nextVal * 10) / 10];
      });

      setGpuLoadHistory(prev => {
        const nextVal = Math.min(100, Math.max(2, prev[prev.length - 1] + (Math.random() * 14 - 7)));
        return [...prev.slice(1), Math.round(nextVal)];
      });

      setGpuTempHistory(prev => {
        const nextVal = Math.min(85, Math.max(40, prev[prev.length - 1] + (Math.random() * 2 - 0.9)));
        return [...prev.slice(1), Math.round(nextVal * 10) / 10];
      });

      setTotalReadings(prev => prev + 12);
      setDbSizeBytes(prev => prev + 384);
    }, pollingRateMs);

    return () => clearInterval(timer);
  }, [pollingRateMs]);

  // Generate downsampled data points based on time range
  const historyData = useMemo(() => {
    const pointsCount = selectedTimeRange === '5m' ? 30 : selectedTimeRange === '15m' ? 30 : 24;
    const baseVal = selectedSensor.includes('Temp') ? 54 : selectedSensor.includes('Load') ? 35 : 3800;
    const spread = selectedSensor.includes('Temp') ? 14 : selectedSensor.includes('Load') ? 25 : 400;

    return Array.from({ length: pointsCount }, (_, i) => {
      const avg = Math.round((baseVal + Math.sin(i * 0.4) * (spread * 0.5) + (Math.random() * 4 - 2)) * 10) / 10;
      const min = Math.round((avg - Math.random() * (spread * 0.25)) * 10) / 10;
      const max = Math.round((avg + Math.random() * (spread * 0.3)) * 10) / 10;
      const count = selectedTimeRange === '5m' ? 10 : selectedTimeRange === '15m' ? 30 : 120;
      const timeLabel = `${String(14 - Math.floor((pointsCount - i) / 10)).padStart(2, '0')}:${String(((i * 2) % 60)).padStart(2, '0')}`;
      return { time: timeLabel, min, max, avg, count } as HistoricalPoint;
    });
  }, [selectedSensor, selectedTimeRange]);

  // Sparkline SVG generator
  const renderSparkline = (data: number[], color: string, height = 36, width = 120) => {
    if (data.length < 2) return null;
    const min = Math.min(...data);
    const max = Math.max(...data);
    const range = max - min || 1;

    const points = data
      .map((val, idx) => {
        const x = (idx / (data.length - 1)) * width;
        const y = height - ((val - min) / range) * (height - 6) - 3;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');

    return (
      <svg width={width} height={height} className="overflow-visible">
        <polyline
          fill="none"
          stroke={color}
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          points={points}
        />
      </svg>
    );
  };

  const handlePurge = () => {
    setTotalReadings(Math.round(totalReadings * 0.72));
    setDbSizeBytes(Math.round(dbSizeBytes * 0.75));
    setStatusMessage(`Manual retention purge completed. Freed ${(dbSizeBytes * 0.25 / 1024 / 1024).toFixed(2)} MB.`);
  };

  return (
    <div className="flex h-screen w-screen bg-[#1F1F1F] text-[#E0E0E0] font-sans overflow-hidden select-none">
      {/* Fluent Left Navigation Bar */}
      <aside className="w-64 bg-[#181818] border-r border-[#2C2C2C] flex flex-col justify-between p-3 shrink-0">
        <div>
          {/* Workstation App Header */}
          <div className="flex items-center gap-3 px-3 py-3 mb-3 border-b border-[#2C2C2C]">
            <div className="w-8 h-8 rounded bg-[#0078D4] flex items-center justify-center text-white font-bold shadow">
              <ShieldCheck className="w-5 h-5" />
            </div>
            <div>
              <div className="text-sm font-semibold text-white tracking-wide">PC SENTINEL</div>
              <div className="text-[10px] text-[#FFB900] font-semibold flex items-center gap-1">
                <span className="w-1.5 h-1.5 rounded-full bg-[#FFB900] inline-block"></span>
                PREVIEW DEMO MODE
              </div>
            </div>
          </div>

          {/* Navigation Links */}
          <nav className="space-y-1">
            <button
              onClick={() => setActiveTab('dashboard')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                activeTab === 'dashboard'
                  ? 'bg-[#2D2D2D] text-white border-l-2 border-[#0078D4]'
                  : 'text-[#9E9E9E] hover:bg-[#232323] hover:text-white'
              }`}
            >
              <Activity className="w-4 h-4" />
              Dashboard
            </button>

            <button
              onClick={() => setActiveTab('hardware')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                activeTab === 'hardware'
                  ? 'bg-[#2D2D2D] text-white border-l-2 border-[#0078D4]'
                  : 'text-[#9E9E9E] hover:bg-[#232323] hover:text-white'
              }`}
            >
              <Layers className="w-4 h-4" />
              Hardware Tree
            </button>

            <button
              onClick={() => setActiveTab('history')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                activeTab === 'history'
                  ? 'bg-[#2D2D2D] text-white border-l-2 border-[#0078D4]'
                  : 'text-[#9E9E9E] hover:bg-[#232323] hover:text-white'
              }`}
            >
              <Clock className="w-4 h-4" />
              History &amp; Analytics
            </button>

            <button
              onClick={() => setActiveTab('alerts')}
              className={`w-full flex items-center justify-between px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                activeTab === 'alerts'
                  ? 'bg-[#2D2D2D] text-white border-l-2 border-[#0078D4]'
                  : 'text-[#9E9E9E] hover:bg-[#232323] hover:text-white'
              }`}
            >
              <div className="flex items-center gap-3">
                <AlertTriangle className="w-4 h-4 text-[#FFB900]" />
                Observations
              </div>
              {observations.length > 0 && (
                <span className="text-[11px] bg-[#3B2E00] text-[#FFB900] px-1.5 py-0.5 rounded font-bold">
                  {observations.length}
                </span>
              )}
            </button>

            <button
              onClick={() => setActiveTab('settings')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                activeTab === 'settings'
                  ? 'bg-[#2D2D2D] text-white border-l-2 border-[#0078D4]'
                  : 'text-[#9E9E9E] hover:bg-[#232323] hover:text-white'
              }`}
            >
              <SettingsIcon className="w-4 h-4" />
              Settings &amp; Storage
            </button>

            <button
              onClick={() => setActiveTab('native-guide')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                activeTab === 'native-guide'
                  ? 'bg-[#2D2D2D] text-[#0078D4] border-l-2 border-[#0078D4]'
                  : 'text-[#0078D4] hover:bg-[#232323]'
              }`}
            >
              <Monitor className="w-4 h-4" />
              Run on Your PC
            </button>
          </nav>
        </div>

        {/* Bottom System Status Badge */}
        <div className="bg-[#202020] rounded-lg p-3 border border-[#2D2D2D] text-xs space-y-1.5">
          <div className="flex justify-between text-[#8A8886]">
            <span>Environment:</span>
            <span className="text-[#FFB900] font-mono">Web Sandbox</span>
          </div>
          <div className="flex justify-between text-[#8A8886]">
            <span>Native Core:</span>
            <span className="text-[#107C41] font-mono">C# / WinUI 3</span>
          </div>
          <div className="flex justify-between text-[#8A8886]">
            <span>SQLite Schema:</span>
            <span className="text-white font-mono">M2 WAL Active</span>
          </div>
        </div>
      </aside>

      {/* Main Content Workspace */}
      <main className="flex-1 flex flex-col overflow-hidden bg-[#1C1C1C]">
        {/* Workspace Title Bar with Sandbox Notice */}
        <header className="h-14 border-b border-[#2C2C2C] bg-[#202020] px-6 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-4">
            <span className="text-xs font-semibold uppercase tracking-wider text-[#8A8886]">
              {activeTab === 'dashboard' && 'Telemetry Overview [Web Preview Mode]'}
              {activeTab === 'hardware' && 'Component Sensor Tree [Web Preview Mode]'}
              {activeTab === 'history' && 'Historical Aggregates & Downsampling'}
              {activeTab === 'alerts' && 'Deterministic Trend Analysis'}
              {activeTab === 'settings' && 'System Configuration & Retention'}
              {activeTab === 'native-guide' && 'Native Windows Execution Guide'}
            </span>
          </div>
          <div className="flex items-center gap-3 text-xs">
            <button
              onClick={() => setActiveTab('native-guide')}
              className="bg-[#262626] hover:bg-[#303030] text-[#0078D4] px-2.5 py-1 rounded border border-[#3A3A3A] font-medium flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <Info className="w-3.5 h-3.5" />
              Why Demo Data?
            </button>
            <span className="bg-[#282828] text-[#8A8886] px-2.5 py-1 rounded border border-[#333333] flex items-center gap-1.5 font-mono">
              <Database className="w-3.5 h-3.5 text-[#0078D4]" />
              {(dbSizeBytes / 1024 / 1024).toFixed(2)} MB
            </span>
          </div>
        </header>

        {/* Global Architecture Notice Banner */}
        <div className="bg-[#2D2A1E] border-b border-[#4D4524] px-6 py-2.5 flex items-center justify-between text-xs text-[#E5C972]">
          <div className="flex items-center gap-2">
            <AlertCircle className="w-4 h-4 text-[#FFB900] shrink-0" />
            <span>
              <strong>Web Sandbox Notice:</strong> A web browser running in a remote cloud container cannot access your physical PC's ring-0 hardware sensors. The hardware models shown below are demonstration profiles.
            </span>
          </div>
          <button
            onClick={() => setActiveTab('native-guide')}
            className="text-white bg-[#57481A] hover:bg-[#6D5B20] px-2.5 py-1 rounded font-medium transition-colors shrink-0 ml-4 flex items-center gap-1"
          >
            How to monitor your real PC <ChevronRight className="w-3 h-3" />
          </button>
        </div>

        {/* Scrollable Workspace Body */}
        <div className="flex-1 overflow-y-auto p-6">
          {/* NATIVE GUIDE TAB */}
          {activeTab === 'native-guide' && (
            <div className="max-w-4xl space-y-6">
              <div className="bg-[#202020] p-6 rounded-xl border border-[#2E2E2E] space-y-4">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-lg bg-[#0078D4]/20 text-[#0078D4] flex items-center justify-center">
                    <Monitor className="w-6 h-6" />
                  </div>
                  <div>
                    <h2 className="text-base font-bold text-white">How PC Sentinel Reads Your Real Hardware</h2>
                    <p className="text-xs text-[#8A8886]">
                      Architecture explanation and steps to launch the native desktop monitor on your Windows PC.
                    </p>
                  </div>
                </div>

                <div className="text-xs text-[#B0B0B0] leading-relaxed space-y-3 pt-2">
                  <p>
                    <strong>Why did you see a 13900K or 4090?</strong><br />
                    Web browsers run in a sandboxed security model and have no access to your CPU registers, motherboard SMBIOS, or GPU drivers. When running in the AI Studio web preview, PC Sentinel runs on a Linux container and presents mock/demonstration profiles so you can test the UI, SQLite persistence, downsampled graphs, and alert engine.
                  </p>
                  <p>
                    <strong>The Real Windows Application:</strong><br />
                    The solution in this repository (<code className="bg-[#181818] px-1.5 py-0.5 rounded text-white">src/PcSentinel.App</code>) is a native <strong>C# .NET 9 + WinUI 3</strong> desktop application that uses <strong>LibreHardwareMonitorLib</strong> to load kernel-level ring-0 drivers on Windows. When you run it locally on your computer with Administrator privileges, it reads your <strong>actual physical CPU, GPU, RAM, motherboard, fans, voltages, and NVMe drives</strong> with zero fabrication.
                  </p>
                </div>

                <div className="bg-[#181818] p-4 rounded-lg border border-[#2A2A2A] space-y-2">
                  <div className="flex items-center gap-2 text-xs font-semibold text-white">
                    <Terminal className="w-4 h-4 text-[#107C41]" />
                    Run Native WinUI 3 Desktop App on Windows:
                  </div>
                  <pre className="bg-[#101010] p-3 rounded text-xs text-[#70C070] font-mono overflow-x-auto">
{`# 1. Clone or export the project to your Windows 10/11 machine
# 2. Open a PowerShell terminal as Administrator
# 3. Build and launch the native desktop application:
dotnet run --project src/PcSentinel.App/PcSentinel.App.csproj`}
                  </pre>
                  <div className="text-[11px] text-[#8A8886]">
                    * Requires .NET 9 SDK and Windows App SDK runtime on Windows 10 (version 1809+) or Windows 11.
                  </div>
                </div>

                <div className="border-t border-[#2D2D2D] pt-4">
                  <div className="text-xs font-semibold text-white mb-2">Configure Preview Demo Profile:</div>
                  <p className="text-xs text-[#8A8886] mb-3">
                    You can customize the preview profile to match your actual hardware specs for testing the UI:
                  </p>
                  <div className="grid grid-cols-2 gap-3 text-xs">
                    <div>
                      <label className="block text-[#8A8886] mb-1">Your CPU Model:</label>
                      <input
                        type="text"
                        value={cpuModel}
                        onChange={e => setCpuModel(e.target.value)}
                        className="w-full bg-[#181818] border border-[#333333] rounded px-2.5 py-1.5 text-white"
                      />
                    </div>
                    <div>
                      <label className="block text-[#8A8886] mb-1">Your GPU Model:</label>
                      <input
                        type="text"
                        value={gpuModel}
                        onChange={e => setGpuModel(e.target.value)}
                        className="w-full bg-[#181818] border border-[#333333] rounded px-2.5 py-1.5 text-white"
                      />
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* DASHBOARD TAB */}
          {activeTab === 'dashboard' && (
            <div className="space-y-6">
              {/* Hardware Summary Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                {/* CPU Card */}
                <div className="bg-[#242424] p-4 rounded-xl border border-[#303030] shadow-sm flex flex-col justify-between">
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2 text-[#0078D4]">
                      <Cpu className="w-5 h-5" />
                      <span className="font-semibold text-white text-sm">Processor</span>
                    </div>
                    <span className="text-[10px] bg-[#181818] text-[#8A8886] px-1.5 py-0.5 rounded font-mono truncate max-w-[120px]">
                      {cpuModel}
                    </span>
                  </div>

                  <div className="flex items-end justify-between my-2">
                    <div>
                      <div className="text-2xl font-bold text-white tracking-tight">
                        {cpuLoadHistory[cpuLoadHistory.length - 1]}%
                      </div>
                      <div className="text-xs text-[#8A8886]">
                        Temp: {cpuTempHistory[cpuTempHistory.length - 1]}°C
                      </div>
                    </div>
                    {/* Live Sparkline */}
                    <div>
                      {renderSparkline(cpuLoadHistory, '#0078D4')}
                      <div className="text-[10px] text-right text-[#666666] mt-1 font-mono">30s Load Trend</div>
                    </div>
                  </div>

                  <div className="text-[11px] text-[#777777] border-t border-[#2D2D2D] pt-2 flex justify-between">
                    <span>Power: 105.4 W</span>
                    <span>Clock: 4.85 GHz</span>
                  </div>
                </div>

                {/* GPU Card */}
                <div className="bg-[#242424] p-4 rounded-xl border border-[#303030] shadow-sm flex flex-col justify-between">
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2 text-[#107C41]">
                      <Activity className="w-5 h-5" />
                      <span className="font-semibold text-white text-sm">Graphics</span>
                    </div>
                    <span className="text-[10px] bg-[#181818] text-[#8A8886] px-1.5 py-0.5 rounded font-mono truncate max-w-[120px]">
                      {gpuModel}
                    </span>
                  </div>

                  <div className="flex items-end justify-between my-2">
                    <div>
                      <div className="text-2xl font-bold text-white tracking-tight">
                        {gpuLoadHistory[gpuLoadHistory.length - 1]}%
                      </div>
                      <div className="text-xs text-[#8A8886]">
                        Temp: {gpuTempHistory[gpuTempHistory.length - 1]}°C
                      </div>
                    </div>
                    {/* Live Sparkline */}
                    <div>
                      {renderSparkline(gpuLoadHistory, '#107C41')}
                      <div className="text-[10px] text-right text-[#666666] mt-1 font-mono">30s Load Trend</div>
                    </div>
                  </div>

                  <div className="text-[11px] text-[#777777] border-t border-[#2D2D2D] pt-2 flex justify-between">
                    <span>Hotspot: {(gpuTempHistory[gpuTempHistory.length - 1] + 7.2).toFixed(1)}°C</span>
                    <span>Fan: 1,350 RPM</span>
                  </div>
                </div>

                {/* Memory Card */}
                <div className="bg-[#242424] p-4 rounded-xl border border-[#303030] shadow-sm flex flex-col justify-between">
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2 text-[#D83B01]">
                      <Layers className="w-5 h-5" />
                      <span className="font-semibold text-white text-sm">Memory</span>
                    </div>
                    <span className="text-[10px] bg-[#181818] text-[#8A8886] px-1.5 py-0.5 rounded font-mono truncate max-w-[120px]">
                      {ramSpec}
                    </span>
                  </div>

                  <div className="flex items-end justify-between my-2">
                    <div>
                      <div className="text-2xl font-bold text-white tracking-tight">12.8 GB</div>
                      <div className="text-xs text-[#8A8886]">Util: 40.0% of 32 GB</div>
                    </div>
                    <div>
                      {renderSparkline([40.1, 40.2, 40.0, 40.0, 40.1, 40.0, 40.0], '#D83B01')}
                      <div className="text-[10px] text-right text-[#666666] mt-1 font-mono">Stable Profile</div>
                    </div>
                  </div>

                  <div className="text-[11px] text-[#777777] border-t border-[#2D2D2D] pt-2 flex justify-between">
                    <span>Available: 19.2 GB</span>
                    <span>Speed: 6000 MT/s</span>
                  </div>
                </div>

                {/* Storage Card */}
                <div className="bg-[#242424] p-4 rounded-xl border border-[#303030] shadow-sm flex flex-col justify-between">
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2 text-[#8764B8]">
                      <HardDrive className="w-5 h-5" />
                      <span className="font-semibold text-white text-sm">NVMe Storage</span>
                    </div>
                    <span className="text-[10px] bg-[#181818] text-[#8A8886] px-1.5 py-0.5 rounded font-mono truncate max-w-[120px]">
                      {storageModel}
                    </span>
                  </div>

                  <div className="flex items-end justify-between my-2">
                    <div>
                      <div className="text-2xl font-bold text-white tracking-tight">43°C</div>
                      <div className="text-xs text-[#8A8886]">Activity: 2.8%</div>
                    </div>
                    <div>
                      {renderSparkline([42, 43, 43, 44, 43, 43, 43], '#8764B8')}
                      <div className="text-[10px] text-right text-[#666666] mt-1 font-mono">Drive Temp 1</div>
                    </div>
                  </div>

                  <div className="text-[11px] text-[#777777] border-t border-[#2D2D2D] pt-2 flex justify-between">
                    <span>Used: 620 GB / 2.0 TB</span>
                    <span>Health: 100%</span>
                  </div>
                </div>
              </div>

              {/* Persistence Stream Status Banner */}
              <div className="bg-[#202020] border border-[#2D2D2D] p-4 rounded-xl flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-lg bg-[#2A3440] text-[#0078D4] flex items-center justify-center">
                    <Database className="w-5 h-5" />
                  </div>
                  <div>
                    <div className="text-sm font-semibold text-white">Local SQLite Persistence Engine (Milestone 2)</div>
                    <div className="text-xs text-[#8A8886]">{statusMessage}</div>
                  </div>
                </div>
                <div className="flex items-center gap-4 text-xs font-mono">
                  <span className="text-[#0078D4]">Queue: 0 pending</span>
                  <span className="text-[#107C41]">Batches: 100% written</span>
                </div>
              </div>
            </div>
          )}

          {/* HARDWARE TOPOLOGY TAB */}
          {activeTab === 'hardware' && (
            <div className="space-y-4">
              <div className="flex items-center justify-between bg-[#202020] p-3 rounded-lg border border-[#2E2E2E]">
                <div className="relative flex-1 max-w-md">
                  <Search className="w-4 h-4 text-[#777777] absolute left-3 top-2.5" />
                  <input
                    type="text"
                    placeholder="Search sensor (e.g., Temp, Clock, Power, Fan)..."
                    value={hardwareSearch}
                    onChange={e => setHardwareSearch(e.target.value)}
                    className="w-full bg-[#181818] border border-[#333333] rounded-md pl-9 pr-3 py-1.5 text-xs text-white focus:outline-none focus:border-[#0078D4]"
                  />
                </div>
                <div className="text-xs text-[#8A8886]">
                  Normalized sensors: 4 devices, 28 sensors
                </div>
              </div>

              {/* Sensor Tree Table */}
              <div className="bg-[#202020] rounded-xl border border-[#2E2E2E] overflow-hidden">
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="bg-[#272727] text-[#8A8886] border-b border-[#303030]">
                      <th className="p-3">Device / Sensor</th>
                      <th className="p-3">Type</th>
                      <th className="p-3">Live Value</th>
                      <th className="p-3">Min</th>
                      <th className="p-3">Max</th>
                      <th className="p-3">Unit</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#282828] font-mono">
                    <tr className="hover:bg-[#252525]">
                      <td className="p-3 font-sans font-medium text-white">{cpuModel} &gt; Package Temp</td>
                      <td className="p-3 text-[#0078D4]">Temperature</td>
                      <td className="p-3 text-white font-bold">{cpuTempHistory[cpuTempHistory.length - 1]}</td>
                      <td className="p-3 text-[#8A8886]">38.0</td>
                      <td className="p-3 text-[#FFB900]">82.4</td>
                      <td className="p-3 text-[#8A8886]">°C</td>
                    </tr>
                    <tr className="hover:bg-[#252525]">
                      <td className="p-3 font-sans font-medium text-white">{cpuModel} &gt; CPU Total Load</td>
                      <td className="p-3 text-[#107C41]">Load</td>
                      <td className="p-3 text-white font-bold">{cpuLoadHistory[cpuLoadHistory.length - 1]}</td>
                      <td className="p-3 text-[#8A8886]">4.2</td>
                      <td className="p-3 text-[#FFB900]">100.0</td>
                      <td className="p-3 text-[#8A8886]">%</td>
                    </tr>
                    <tr className="hover:bg-[#252525]">
                      <td className="p-3 font-sans font-medium text-white">{cpuModel} &gt; Package Power</td>
                      <td className="p-3 text-[#D83B01]">Power</td>
                      <td className="p-3 text-white font-bold">104.6</td>
                      <td className="p-3 text-[#8A8886]">28.0</td>
                      <td className="p-3 text-[#FFB900]">142.0</td>
                      <td className="p-3 text-[#8A8886]">W</td>
                    </tr>
                    <tr className="hover:bg-[#252525]">
                      <td className="p-3 font-sans font-medium text-white">{gpuModel} &gt; Core Temp</td>
                      <td className="p-3 text-[#0078D4]">Temperature</td>
                      <td className="p-3 text-white font-bold">{gpuTempHistory[gpuTempHistory.length - 1]}</td>
                      <td className="p-3 text-[#8A8886]">34.5</td>
                      <td className="p-3 text-[#FFB900]">71.0</td>
                      <td className="p-3 text-[#8A8886]">°C</td>
                    </tr>
                    <tr className="hover:bg-[#252525]">
                      <td className="p-3 font-sans font-medium text-white">{gpuModel} &gt; Core Load</td>
                      <td className="p-3 text-[#107C41]">Load</td>
                      <td className="p-3 text-white font-bold">{gpuLoadHistory[gpuLoadHistory.length - 1]}</td>
                      <td className="p-3 text-[#8A8886]">0.0</td>
                      <td className="p-3 text-[#FFB900]">99.0</td>
                      <td className="p-3 text-[#8A8886]">%</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* HISTORY & ANALYTICS TAB (MILESTONE 2 CORE) */}
          {activeTab === 'history' && (
            <div className="space-y-6">
              {/* Controls Bar */}
              <div className="bg-[#202020] p-4 rounded-xl border border-[#2E2E2E] flex flex-wrap items-center justify-between gap-4">
                <div className="flex items-center gap-3">
                  <select
                    value={selectedDevice}
                    onChange={e => setSelectedDevice(e.target.value)}
                    className="bg-[#181818] border border-[#333333] text-xs text-white rounded px-3 py-1.5 focus:outline-none"
                  >
                    <option value="cpu">{cpuModel}</option>
                    <option value="gpu">{gpuModel}</option>
                  </select>

                  <select
                    value={selectedSensor}
                    onChange={e => setSelectedSensor(e.target.value)}
                    className="bg-[#181818] border border-[#333333] text-xs text-white rounded px-3 py-1.5 focus:outline-none"
                  >
                    <option value="Core Max Temp">Core Max Temperature (°C)</option>
                    <option value="Package Temp">Package Temperature (°C)</option>
                    <option value="Total Load">Total Utilization (%)</option>
                    <option value="Core Clock">Core Clock (MHz)</option>
                  </select>
                </div>

                {/* Time Range Selector */}
                <div className="flex items-center bg-[#181818] p-1 rounded-md border border-[#303030]">
                  {(['5m', '15m', '1h', '6h', '24h', '7d'] as TimeRange[]).map(range => (
                    <button
                      key={range}
                      onClick={() => setSelectedTimeRange(range)}
                      className={`px-3 py-1 text-xs rounded font-medium transition-colors ${
                        selectedTimeRange === range
                          ? 'bg-[#0078D4] text-white shadow'
                          : 'text-[#8A8886] hover:text-white'
                      }`}
                    >
                      {range}
                    </button>
                  ))}
                </div>
              </div>

              {/* Historical Telemetry Graph */}
              <div className="bg-[#202020] p-6 rounded-xl border border-[#2E2E2E] space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-sm font-semibold text-white">{selectedSensor} — Historical Trend</h3>
                    <p className="text-xs text-[#8A8886]">
                      Bucket resolution:{' '}
                      {selectedTimeRange === '5m' ? '1s raw samples' : selectedTimeRange === '15m' ? '5s averages' : '1m rollups'}
                    </p>
                  </div>
                  <div className="flex items-center gap-4 text-xs font-mono">
                    <span className="flex items-center gap-1.5 text-[#0078D4]">
                      <span className="w-2.5 h-2.5 rounded-full bg-[#0078D4]"></span> Average
                    </span>
                    <span className="flex items-center gap-1.5 text-[#666666]">
                      <span className="w-2.5 h-2.5 rounded bg-[#333333]"></span> Min/Max Envelope
                    </span>
                  </div>
                </div>

                {/* Interactive SVG Downsampled Graph */}
                <div className="h-64 w-full relative bg-[#181818] rounded-lg border border-[#282828] p-4 flex items-end">
                  <svg className="w-full h-full overflow-visible" viewBox="0 0 800 200" preserveAspectRatio="none">
                    {/* Grid lines */}
                    <line x1="0" y1="50" x2="800" y2="50" stroke="#252525" strokeDasharray="3,3" />
                    <line x1="0" y1="100" x2="800" y2="100" stroke="#252525" strokeDasharray="3,3" />
                    <line x1="0" y1="150" x2="800" y2="150" stroke="#252525" strokeDasharray="3,3" />

                    {/* Min/Max Area Envelope */}
                    <polygon
                      fill="rgba(0, 120, 212, 0.1)"
                      points={
                        historyData
                          .map((pt, i) => {
                            const x = (i / (historyData.length - 1)) * 800;
                            const y = 200 - (pt.max / 100) * 190 - 5;
                            return `${x},${y}`;
                          })
                          .join(' ') +
                        ' ' +
                        historyData
                          .slice()
                          .reverse()
                          .map((pt, i) => {
                            const x = ((historyData.length - 1 - i) / (historyData.length - 1)) * 800;
                            const y = 200 - (pt.min / 100) * 190 - 5;
                            return `${x},${y}`;
                          })
                          .join(' ')
                      }
                    />

                    {/* Average Polyline */}
                    <polyline
                      fill="none"
                      stroke="#0078D4"
                      strokeWidth="2.5"
                      points={historyData
                        .map((pt, i) => {
                          const x = (i / (historyData.length - 1)) * 800;
                          const y = 200 - (pt.avg / 100) * 190 - 5;
                          return `${x},${y}`;
                        })
                        .join(' ')}
                    />
                  </svg>
                </div>

                {/* Summary Metrics */}
                <div className="grid grid-cols-4 gap-4 pt-2">
                  <div className="bg-[#181818] p-3 rounded-lg border border-[#2A2A2A] text-center">
                    <div className="text-[11px] text-[#8A8886]">Sample Minimum</div>
                    <div className="text-base font-bold text-white font-mono">
                      {Math.min(...historyData.map(d => d.min)).toFixed(1)}
                    </div>
                  </div>
                  <div className="bg-[#181818] p-3 rounded-lg border border-[#2A2A2A] text-center">
                    <div className="text-[11px] text-[#8A8886]">Sample Maximum</div>
                    <div className="text-base font-bold text-[#FFB900] font-mono">
                      {Math.max(...historyData.map(d => d.max)).toFixed(1)}
                    </div>
                  </div>
                  <div className="bg-[#181818] p-3 rounded-lg border border-[#2A2A2A] text-center">
                    <div className="text-[11px] text-[#8A8886]">Period Average</div>
                    <div className="text-base font-bold text-[#0078D4] font-mono">
                      {(historyData.reduce((acc, d) => acc + d.avg, 0) / historyData.length).toFixed(1)}
                    </div>
                  </div>
                  <div className="bg-[#181818] p-3 rounded-lg border border-[#2A2A2A] text-center">
                    <div className="text-[11px] text-[#8A8886]">Aggregated Samples</div>
                    <div className="text-base font-bold text-[#107C41] font-mono">
                      {historyData.reduce((acc, d) => acc + d.count, 0).toLocaleString()}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* ALERTS / OBSERVATIONS TAB */}
          {activeTab === 'alerts' && (
            <div className="space-y-4">
              <div className="flex items-center justify-between bg-[#202020] p-4 rounded-xl border border-[#2E2E2E]">
                <div>
                  <h3 className="text-sm font-semibold text-white">Deterministic Observation Stream</h3>
                  <p className="text-xs text-[#8A8886]">
                    Real-time detection of thermal spikes and sustained utilization patterns.
                  </p>
                </div>
                <button
                  onClick={() => setObservations([])}
                  className="px-3 py-1.5 bg-[#282828] hover:bg-[#303030] text-xs text-white rounded-md border border-[#3A3A3A] flex items-center gap-1.5 transition-colors"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                  Clear Observations
                </button>
              </div>

              <div className="space-y-2">
                {observations.length === 0 ? (
                  <div className="bg-[#202020] p-8 text-center text-[#8A8886] rounded-xl border border-[#2E2E2E]">
                    No active thermal or utilization anomalies detected.
                  </div>
                ) : (
                  observations.map(obs => (
                    <div
                      key={obs.id}
                      className="bg-[#242424] p-4 rounded-lg border border-[#333333] flex items-start justify-between gap-4"
                    >
                      <div className="flex items-start gap-3">
                        <AlertTriangle className="w-5 h-5 text-[#FFB900] shrink-0 mt-0.5" />
                        <div className="space-y-1">
                          <div className="text-xs font-semibold text-white">{obs.message}</div>
                          <div className="text-[11px] text-[#8A8886] flex items-center gap-2">
                            <span>{obs.hardware}</span>
                            <span>•</span>
                            <span>{obs.sensor}</span>
                            <span>•</span>
                            <span className="text-[#FFB900] font-medium">{obs.severity}</span>
                          </div>
                        </div>
                      </div>
                      <span className="text-xs text-[#666666] font-mono">{obs.timestamp}</span>
                    </div>
                  ))
                )}
              </div>
            </div>
          )}

          {/* SETTINGS & RETENTION TAB */}
          {activeTab === 'settings' && (
            <div className="space-y-6 max-w-4xl">
              {/* Cadence */}
              <div className="bg-[#202020] p-5 rounded-xl border border-[#2E2E2E] space-y-4">
                <div className="flex items-center gap-2 text-xs font-bold text-[#8A8886] uppercase tracking-wider">
                  <Sliders className="w-4 h-4 text-[#0078D4]" />
                  Hardware Polling Engine
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <div className="text-sm font-semibold text-white">Sensor Polling Cadence</div>
                    <div className="text-xs text-[#8A8886]">
                      Frequency at which LibreHardwareMonitor reads hardware sensors.
                    </div>
                  </div>
                  <select
                    value={pollingRateMs}
                    onChange={e => setPollingRateMs(Number(e.target.value))}
                    className="bg-[#181818] border border-[#333333] text-xs text-white rounded px-3 py-1.5 focus:outline-none"
                  >
                    <option value={250}>250 ms (4.0 Hz)</option>
                    <option value={500}>500 ms (2.0 Hz)</option>
                    <option value={1000}>1000 ms (1.0 Hz - Recommended)</option>
                    <option value={2000}>2000 ms (0.5 Hz)</option>
                  </select>
                </div>
              </div>

              {/* Retention */}
              <div className="bg-[#202020] p-5 rounded-xl border border-[#2E2E2E] space-y-4">
                <div className="flex items-center gap-2 text-xs font-bold text-[#8A8886] uppercase tracking-wider">
                  <Database className="w-4 h-4 text-[#107C41]" />
                  Local SQLite Persistence &amp; Retention
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <div className="text-sm font-semibold text-white">Data Retention Period</div>
                    <div className="text-xs text-[#8A8886]">
                      Telemetry readings older than this threshold will be purged by the background retention worker.
                    </div>
                  </div>
                  <select
                    value={retentionDays}
                    onChange={e => setRetentionDays(Number(e.target.value))}
                    className="bg-[#181818] border border-[#333333] text-xs text-white rounded px-3 py-1.5 focus:outline-none"
                  >
                    <option value={1}>1 Day</option>
                    <option value={3}>3 Days</option>
                    <option value={7}>7 Days (Default)</option>
                    <option value={14}>14 Days</option>
                    <option value={30}>30 Days</option>
                    <option value={90}>90 Days</option>
                  </select>
                </div>

                <div className="border-t border-[#2A2A2A] pt-4 space-y-3">
                  <div className="text-xs font-medium text-white">Storage Diagnostics</div>
                  <div className="grid grid-cols-2 gap-4 text-xs">
                    <div className="bg-[#181818] p-3 rounded border border-[#282828]">
                      <div className="text-[#8A8886]">Total Telemetry Samples</div>
                      <div className="text-lg font-bold text-[#0078D4] font-mono mt-1">
                        {totalReadings.toLocaleString()}
                      </div>
                    </div>
                    <div className="bg-[#181818] p-3 rounded border border-[#282828]">
                      <div className="text-[#8A8886]">Database Disk Footprint</div>
                      <div className="text-lg font-bold text-white font-mono mt-1">
                        {(dbSizeBytes / 1024 / 1024).toFixed(2)} MB
                      </div>
                    </div>
                  </div>
                </div>

                <div className="pt-2 flex items-center gap-3">
                  <button
                    onClick={handlePurge}
                    className="px-4 py-2 bg-[#0078D4] hover:bg-[#106EBE] text-xs font-semibold text-white rounded-md flex items-center gap-2 transition-colors"
                  >
                    <Trash2 className="w-4 h-4" />
                    Run Retention Purge Now
                  </button>
                  <button
                    onClick={() => setStatusMessage('Storage metrics refreshed from disk.')}
                    className="px-4 py-2 bg-[#262626] hover:bg-[#303030] text-xs text-[#E0E0E0] rounded-md border border-[#333333] flex items-center gap-2 transition-colors"
                  >
                    <RefreshCw className="w-4 h-4" />
                    Refresh Stats
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
