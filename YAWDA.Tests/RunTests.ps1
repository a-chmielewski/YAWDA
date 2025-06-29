# YAWDA Test Runner Script
# Implements Step 14 of the implementation plan - comprehensive testing

param(
    [switch]$Unit,
    [switch]$Integration,
    [switch]$Performance,
    [switch]$Coverage,
    [switch]$All
)

# Default to running all tests if no specific category is selected
if (-not ($Unit -or $Integration -or $Performance -or $Coverage)) {
    $All = $true
}

$ErrorActionPreference = "Stop"

Write-Host "YAWDA Comprehensive Test Suite" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

# Test project path
$testProjectPath = "YAWDA.Tests.csproj"

# Create output directories
if (!(Test-Path "TestResults")) {
    New-Item -ItemType Directory -Path "TestResults" -Force | Out-Null
}

if (!(Test-Path "BenchmarkResults")) {
    New-Item -ItemType Directory -Path "BenchmarkResults" -Force | Out-Null
}

# Function to run unit tests
function Invoke-UnitTests {
    Write-Host "Running Unit Tests..." -ForegroundColor Yellow
    
    $filter = "Category!=Integration&Category!=Performance"
    
    Write-Host "Executing: dotnet test $testProjectPath --filter `"$filter`" --logger `"trx;LogFileName=UnitTests.trx`" --results-directory TestResults --verbosity normal" -ForegroundColor Gray
    
    try {
        dotnet test $testProjectPath --filter $filter --logger "trx;LogFileName=UnitTests.trx" --results-directory TestResults --verbosity normal
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Unit tests completed with exit code $LASTEXITCODE"
        } else {
            Write-Host "Unit tests completed successfully" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "Unit test execution failed: $($_.Exception.Message)"
    }
}

# Function to run integration tests
function Invoke-IntegrationTests {
    Write-Host "Running Integration Tests..." -ForegroundColor Yellow
    
    $filter = "Category=Integration|FullyQualifiedName~Integration"
    
    Write-Host "Executing: dotnet test $testProjectPath --filter `"$filter`" --logger `"trx;LogFileName=IntegrationTests.trx`" --results-directory TestResults --verbosity normal" -ForegroundColor Gray
    
    try {
        dotnet test $testProjectPath --filter $filter --logger "trx;LogFileName=IntegrationTests.trx" --results-directory TestResults --verbosity normal
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Integration tests completed with exit code $LASTEXITCODE"
        } else {
            Write-Host "Integration tests completed successfully" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "Integration test execution failed: $($_.Exception.Message)"
    }
}

# Function to run performance tests
function Invoke-PerformanceTests {
    Write-Host "Running Performance Tests..." -ForegroundColor Yellow
    
    $filter = "Category=Performance|FullyQualifiedName~Performance"
    
    Write-Host "Executing: dotnet test $testProjectPath --filter `"$filter`" --logger `"trx;LogFileName=PerformanceTests.trx`" --results-directory TestResults --verbosity normal" -ForegroundColor Gray
    
    try {
        dotnet test $testProjectPath --filter $filter --logger "trx;LogFileName=PerformanceTests.trx" --results-directory TestResults --verbosity normal
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Performance tests completed with exit code $LASTEXITCODE"
        } else {
            Write-Host "Performance tests completed successfully" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "Performance test execution failed: $($_.Exception.Message)"
    }
}

# Main execution logic
try {
    # Clean previous results
    if (Test-Path "TestResults") {
        Remove-Item "TestResults\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    # Run selected test categories
    if ($Unit -or $All) {
        Invoke-UnitTests
    }
    
    if ($Integration -or $All) {
        Invoke-IntegrationTests
    }
    
    if ($Performance -or $All) {
        Invoke-PerformanceTests
    }
    
    Write-Host "Test execution completed!" -ForegroundColor Green
    
} catch {
    Write-Error "Test execution failed: $($_.Exception.Message)"
    exit 1
}

Write-Host "Usage Examples:" -ForegroundColor Cyan
Write-Host "  .\RunTests.ps1 -Unit              # Run only unit tests" -ForegroundColor Gray
Write-Host "  .\RunTests.ps1 -Integration       # Run only integration tests" -ForegroundColor Gray
Write-Host "  .\RunTests.ps1 -Performance       # Run only performance tests" -ForegroundColor Gray
Write-Host "  .\RunTests.ps1 -All               # Run all tests" -ForegroundColor Gray 