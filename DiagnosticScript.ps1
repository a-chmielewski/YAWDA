#!/usr/bin/env pwsh
# YAWDA Diagnostic Script
# This script checks for common issues that can cause the black screen problem

Write-Host "=== YAWDA Diagnostic Script ===" -ForegroundColor Cyan
Write-Host "Checking system requirements and common issues..." -ForegroundColor Yellow
Write-Host ""

# Check Windows version
Write-Host "1. Checking Windows Version..." -ForegroundColor Green
$osVersion = [System.Environment]::OSVersion
Write-Host "   OS Version: $($osVersion.VersionString)"
Write-Host "   Platform: $($osVersion.Platform)"

# Check if Windows 10/11
$version = [System.Environment]::OSVersion.Version
if ($version.Major -lt 10) {
    Write-Host "   [ERROR] Windows 10 or later is required" -ForegroundColor Red
} elseif ($version.Build -lt 19041) {
    Write-Host "   [WARNING] Windows 10 version 2004 (build 19041) or later recommended" -ForegroundColor Yellow
} else {
    Write-Host "   [OK] Windows version is compatible" -ForegroundColor Green
}
Write-Host ""

# Check .NET version
Write-Host "2. Checking .NET Runtime..." -ForegroundColor Green
try {
    $dotnetVersion = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    Write-Host "   Runtime: $dotnetVersion"
    
    # Check for .NET 8.0
    $installedVersions = dotnet --list-runtimes 2>$null
    if ($installedVersions -match "Microsoft.WindowsDesktop.App 8\.") {
        Write-Host "   [OK] .NET 8.0 Desktop Runtime found" -ForegroundColor Green
    } else {
        Write-Host "   [ERROR] .NET 8.0 Desktop Runtime not found" -ForegroundColor Red
        Write-Host "   Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   [ERROR] Unable to detect .NET runtime" -ForegroundColor Red
}
Write-Host ""

# Check Windows App SDK runtime
Write-Host "3. Checking Windows App SDK Runtime..." -ForegroundColor Green
try {
    # Check for Windows App SDK in registry
    $registryPath = "HKLM:\SOFTWARE\Microsoft\WindowsAppSDK"
    if (Test-Path $registryPath) {
        Write-Host "   [OK] Windows App SDK registry entry found" -ForegroundColor Green
    } else {
        Write-Host "   [WARNING] Windows App SDK registry entry not found" -ForegroundColor Yellow
    }
    
    # Check for WinUI 3 runtime files
    $winuiPath = Join-Path $env:ProgramFiles "Microsoft\WindowsAppRuntime"
    if (Test-Path $winuiPath) {
        Write-Host "   [OK] Windows App Runtime files found at: $winuiPath" -ForegroundColor Green
    } else {
        Write-Host "   [ERROR] Windows App Runtime files not found" -ForegroundColor Red
        Write-Host "   Install Windows App SDK Runtime from:" -ForegroundColor Yellow
        Write-Host "   https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   [ERROR] Unable to check Windows App SDK" -ForegroundColor Red
}
Write-Host ""

# Check for Visual C++ Redistributables
Write-Host "4. Checking Visual C++ Redistributables..." -ForegroundColor Green
try {
    $vcRedist = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like "*Visual C++*" }
    if ($vcRedist) {
        Write-Host "   [OK] Visual C++ Redistributables found:" -ForegroundColor Green
        $vcRedist | ForEach-Object { Write-Host "      - $($_.Name)" }
    } else {
        Write-Host "   [WARNING] No Visual C++ Redistributables detected via WMI" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   [WARNING] Unable to check Visual C++ Redistributables" -ForegroundColor Yellow
}
Write-Host ""

# Check YAWDA build output
Write-Host "5. Checking YAWDA Build Output..." -ForegroundColor Green
$buildPath = ".\bin\ARM64\Debug\net8.0-windows10.0.19041.0\win-arm64"
if (Test-Path $buildPath) {
    Write-Host "   [OK] Build output found at: $buildPath" -ForegroundColor Green
    $exePath = Join-Path $buildPath "YAWDA.exe"
    if (Test-Path $exePath) {
        Write-Host "   [OK] YAWDA.exe found" -ForegroundColor Green
        $fileInfo = Get-Item $exePath
        Write-Host "   File size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB"
        Write-Host "   Modified: $($fileInfo.LastWriteTime)"
    } else {
        Write-Host "   [ERROR] YAWDA.exe not found in build output" -ForegroundColor Red
    }
} else {
    Write-Host "   [ERROR] Build output directory not found" -ForegroundColor Red
    Write-Host "   Run 'dotnet build' first" -ForegroundColor Yellow
}
Write-Host ""

# Check for error logs
Write-Host "6. Checking for Error Logs..." -ForegroundColor Green
$logDir = Join-Path $env:LOCALAPPDATA "YAWDA"
if (Test-Path $logDir) {
    Write-Host "   [OK] YAWDA data directory found: $logDir" -ForegroundColor Green
    
    $errorLogs = @("startup_error.log", "mainpage_error.log")
    foreach ($logFile in $errorLogs) {
        $logPath = Join-Path $logDir $logFile
        if (Test-Path $logPath) {
            Write-Host "   [WARNING] Error log found: $logFile" -ForegroundColor Yellow
            $content = Get-Content $logPath -Tail 5
            Write-Host "   Last 5 lines:" -ForegroundColor Yellow
            $content | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
        }
    }
} else {
    Write-Host "   [INFO] No YAWDA data directory found (first run)" -ForegroundColor Blue
}
Write-Host ""

# Recommendations
Write-Host "=== RECOMMENDATIONS ===" -ForegroundColor Cyan
Write-Host "If you're experiencing a black screen issue:" -ForegroundColor Yellow
Write-Host "1. Install Windows App SDK Runtime if missing" -ForegroundColor White
Write-Host "2. Install .NET 8.0 Desktop Runtime if missing" -ForegroundColor White
Write-Host "3. Check error logs in %LOCALAPPDATA%\YAWDA\" -ForegroundColor White
Write-Host "4. Run 'dotnet build' to ensure clean build" -ForegroundColor White
Write-Host "5. Run YAWDA from command line to see console output" -ForegroundColor White
Write-Host ""

Write-Host "=== DIAGNOSTIC COMPLETE ===" -ForegroundColor Cyan 