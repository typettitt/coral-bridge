<#
.SYNOPSIS
    Installs CoralBridge as a Windows Service

.DESCRIPTION
    Builds the CoralBridge service in Release mode and installs it as a Windows Service
    that starts automatically on boot.

.PARAMETER ServiceName
    Name of the Windows Service (default: CoralBridge)

.PARAMETER InstallPath
    Path where the service will be installed (default: C:\CoralBridge)

.PARAMETER Uninstall
    If specified, uninstalls the service instead

.EXAMPLE
    .\install-service.ps1

.EXAMPLE
    .\install-service.ps1 -Uninstall
#>

param(
    [string]$ServiceName = "CoralBridge",
    [string]$InstallPath = "C:\CoralBridge",
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

# Check for admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script requires Administrator privileges. Please run as Administrator."
    exit 1
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectPath = Join-Path $repoRoot "src\CoralBridge.Service"
$modelsPath = Join-Path $repoRoot "models"
$runtimePath = Join-Path $repoRoot "runtime\edgetpu_runtime\libedgetpu\direct\x64_windows"

if ($Uninstall) {
    Write-Host "Uninstalling $ServiceName service..." -ForegroundColor Yellow

    # Stop service if running
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Write-Host "Stopping service..."
            Stop-Service -Name $ServiceName -Force
            Start-Sleep -Seconds 2
        }

        Write-Host "Removing service..."
        sc.exe delete $ServiceName

        Write-Host "Service removed successfully." -ForegroundColor Green
    }
    else {
        Write-Host "Service '$ServiceName' not found." -ForegroundColor Yellow
    }

    exit 0
}

Write-Host "=== CoralBridge Service Installer ===" -ForegroundColor Cyan
Write-Host ""

# Check for .NET 10 SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if (-not $dotnetVersion) {
    Write-Error ".NET SDK not found. Please install .NET 10 SDK."
    exit 1
}
Write-Host "  .NET SDK version: $dotnetVersion" -ForegroundColor Gray

# Build the project
Write-Host ""
Write-Host "Building CoralBridge in Release mode..." -ForegroundColor Yellow
Push-Location $projectPath
try {
    dotnet publish -c Release -o $InstallPath --self-contained false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
}
finally {
    Pop-Location
}
Write-Host "  Build completed successfully" -ForegroundColor Green

# Copy models
Write-Host ""
Write-Host "Copying models..." -ForegroundColor Yellow
$destModelsPath = Join-Path $InstallPath "models"
if (-not (Test-Path $destModelsPath)) {
    New-Item -ItemType Directory -Path $destModelsPath -Force | Out-Null
}
Copy-Item -Path (Join-Path $modelsPath "*") -Destination $destModelsPath -Force -Recurse
Write-Host "  Models copied to $destModelsPath" -ForegroundColor Gray

# Copy Edge TPU DLL
Write-Host ""
Write-Host "Copying Edge TPU runtime..." -ForegroundColor Yellow
$edgetpuDll = Join-Path $runtimePath "edgetpu.dll"
if (Test-Path $edgetpuDll) {
    Copy-Item -Path $edgetpuDll -Destination $InstallPath -Force
    Write-Host "  edgetpu.dll copied" -ForegroundColor Gray
}
else {
    Write-Warning "edgetpu.dll not found at $edgetpuDll"
}

# Check for tensorflowlite_c.dll
$tfliteDll = Join-Path $InstallPath "tensorflowlite_c.dll"
if (-not (Test-Path $tfliteDll)) {
    Write-Host ""
    Write-Warning "tensorflowlite_c.dll not found in output directory!"
    Write-Host "  Please download from: https://github.com/tphakala/tflite_c/releases" -ForegroundColor Yellow
    Write-Host "  And copy to: $InstallPath" -ForegroundColor Yellow
}

# Stop existing service if running
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host ""
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    # Remove existing service
    sc.exe delete $ServiceName 2>$null
    Start-Sleep -Seconds 1
}

# Install the service
Write-Host ""
Write-Host "Installing Windows Service..." -ForegroundColor Yellow
$exePath = Join-Path $InstallPath "CoralBridge.Service.exe"

sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= "CoralBridge Object Detection"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service"
    exit 1
}

sc.exe description $ServiceName "Coral Edge TPU object detection service with DeepStack-compatible API"
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000

Write-Host "  Service installed successfully" -ForegroundColor Green

# Start the service
Write-Host ""
Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName
Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "  Service is running" -ForegroundColor Green
}
else {
    Write-Warning "Service may not have started correctly. Status: $($service.Status)"
}

Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Service Details:" -ForegroundColor White
Write-Host "  Name: $ServiceName"
Write-Host "  Path: $InstallPath"
Write-Host "  URL:  http://localhost:5555"
Write-Host ""
Write-Host "Test the service:" -ForegroundColor White
Write-Host "  curl http://localhost:5555/health"
Write-Host ""
