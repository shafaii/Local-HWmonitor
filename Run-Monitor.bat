@echo off
title PC Sentinel Hardware Monitor
echo ===================================================
echo     Starting PC Sentinel Hardware Monitor (CLI)
echo ===================================================
echo Checking for Administrator privileges...
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [OK] Running as Administrator - Kernel ring-0 sensors enabled!
) else (
    echo [WARNING] Not running as Administrator. Some motherboard/fan sensors may be hidden.
    echo Right-click this file and choose "Run as administrator" for full sensor access.
)
echo.
echo Launching live hardware telemetry stream...
dotnet run --project src/PcSentinel.Cli/PcSentinel.Cli.csproj
pause
