# Debug script for YAWDA to capture detailed output
param(
    [switch]$WaitForExit = $false
)

Write-Host "=== YAWDA Debug Run ===" -ForegroundColor Green
Write-Host "Starting YAWDA with debug output capture..." -ForegroundColor Yellow

# Kill any existing YAWDA processes
$existingProcesses = Get-Process -Name "YAWDA" -ErrorAction SilentlyContinue
if ($existingProcesses) {
    Write-Host "Killing existing YAWDA processes..." -ForegroundColor Yellow
    $existingProcesses | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Start the application with output capture
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = "dotnet"
$startInfo.Arguments = "run"
$startInfo.WorkingDirectory = Get-Location
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $startInfo

# Event handlers for output
$outputBuilder = New-Object System.Text.StringBuilder
$errorBuilder = New-Object System.Text.StringBuilder

$outputHandler = {
    if ($Event.SourceEventArgs.Data) {
        $line = $Event.SourceEventArgs.Data
        [void]$outputBuilder.AppendLine($line)
        Write-Host "OUT: $line" -ForegroundColor Green
    }
}

$errorHandler = {
    if ($Event.SourceEventArgs.Data) {
        $line = $Event.SourceEventArgs.Data
        [void]$errorBuilder.AppendLine($line)
        Write-Host "ERR: $line" -ForegroundColor Red
    }
}

# Register event handlers
Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action $outputHandler | Out-Null
Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action $errorHandler | Out-Null

try {
    # Start the process
    Write-Host "Starting process..." -ForegroundColor Yellow
    $process.Start() | Out-Null
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    
    Write-Host "Process started with PID: $($process.Id)" -ForegroundColor Green
    Write-Host "Waiting for output..." -ForegroundColor Yellow
    
    if ($WaitForExit) {
        # Wait for the process to exit
        $process.WaitForExit()
        Write-Host "Process exited with code: $($process.ExitCode)" -ForegroundColor $(if ($process.ExitCode -eq 0) { "Green" } else { "Red" })
    } else {
        # Wait for a reasonable amount of time or until process exits
        $timeout = 30 # seconds
        $waited = 0
        
        while (-not $process.HasExited -and $waited -lt $timeout) {
            Start-Sleep -Seconds 1
            $waited++
            
            if ($waited % 5 -eq 0) {
                Write-Host "Still running... ($waited/$timeout seconds)" -ForegroundColor Yellow
            }
        }
        
        if ($process.HasExited) {
            Write-Host "Process exited with code: $($process.ExitCode)" -ForegroundColor $(if ($process.ExitCode -eq 0) { "Green" } else { "Red" })
        } else {
            Write-Host "Process is still running after $timeout seconds" -ForegroundColor Green
            Write-Host "Check Task Manager for YAWDA process and system tray for icon" -ForegroundColor Cyan
        }
    }
    
} catch {
    Write-Host "Error starting process: $_" -ForegroundColor Red
} finally {
    # Clean up event handlers
    Get-EventSubscriber | Where-Object { $_.SourceObject -eq $process } | Unregister-Event
    
    if (-not $process.HasExited) {
        Write-Host "Process is still running (PID: $($process.Id))" -ForegroundColor Green
    }
}

Write-Host "`nOutput Summary:" -ForegroundColor Yellow
Write-Host "Standard Output:" -ForegroundColor Green
Write-Host $outputBuilder.ToString()
Write-Host "Standard Error:" -ForegroundColor Red  
Write-Host $errorBuilder.ToString()

Write-Host "`nTo check if app is running:" -ForegroundColor Cyan
Write-Host "Get-Process -Name 'YAWDA' -ErrorAction SilentlyContinue" 