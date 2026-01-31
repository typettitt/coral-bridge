<#
.SYNOPSIS
    Deploys CoralBridge service and CLI tool

.DESCRIPTION
    Stops the running CoralBridge service, rebuilds and deploys the new version,
    and publishes the CLI tool for local use.

.PARAMETER ServiceName
    Name of the Windows Service (default: CoralBridge)

.PARAMETER InstallPath
    Path where the service is installed (default: C:\CoralBridge)

.PARAMETER CliInstallPath
    Path where the CLI tool will be installed (default: %USERPROFILE%\.coralbridge)

.PARAMETER SkipService
    Skip service deployment, only deploy CLI

.PARAMETER SkipCli
    Skip CLI deployment, only deploy service

.PARAMETER AddToPath
    Add CLI install path to user PATH environment variable

.EXAMPLE
    .\deploy.ps1

.EXAMPLE
    .\deploy.ps1 -SkipService

.EXAMPLE
    .\deploy.ps1 -AddToPath
#>

param(
    [string]$ServiceName = "CoralBridge",
    [string]$InstallPath = "C:\CoralBridge",
    [string]$CliInstallPath = "$env:USERPROFILE\.coralbridge",
    [switch]$SkipService,
    [switch]$SkipCli,
    [switch]$AddToPath
)

$ErrorActionPreference = "Stop"

# Get paths
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir
$servicePath = Join-Path $repoRoot "src\CoralBridge.Service"
$cliPath = Join-Path $repoRoot "src\CoralBridge.Cli"
$modelsPath = Join-Path $repoRoot "models"
$runtimePath = Join-Path $repoRoot "runtime\edgetpu_runtime\libedgetpu\direct\x64_windows"

Write-Host ""
Write-Host "  CORAL BRIDGE - DEPLOYMENT" -ForegroundColor Cyan
Write-Host ""

# Check for admin if deploying service
if (-not $SkipService) {
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Error "Service deployment requires Administrator privileges. Run as Administrator or use -SkipService."
        exit 1
    }
}

# Check .NET SDK
Write-Host "[1/6] Checking prerequisites..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if (-not $dotnetVersion) {
    Write-Error ".NET SDK not found. Please install .NET 10 SDK."
    exit 1
}
Write-Host "      .NET SDK: $dotnetVersion" -ForegroundColor Gray

#region Service Deployment
if (-not $SkipService) {
    # Stop service
    Write-Host ""
    Write-Host "[2/6] Stopping service..." -ForegroundColor Yellow
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Stop-Service -Name $ServiceName -Force
            $timeout = 30
            $elapsed = 0
            while ((Get-Service -Name $ServiceName).Status -ne "Stopped" -and $elapsed -lt $timeout) {
                Start-Sleep -Seconds 1
                $elapsed++
                Write-Host "      Waiting for service to stop... ($elapsed sec)" -ForegroundColor Gray
            }
            if ((Get-Service -Name $ServiceName).Status -ne "Stopped") {
                Write-Warning "Service did not stop gracefully, forcing termination..."
                $proc = Get-Process -Name "CoralBridge.Service" -ErrorAction SilentlyContinue
                if ($proc) { $proc | Stop-Process -Force }
                Start-Sleep -Seconds 2
            }
            Write-Host "      Service stopped" -ForegroundColor Green
        }
        else {
            Write-Host "      Service was not running" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "      Service not installed (will create)" -ForegroundColor Gray
    }

    # Build and publish service
    Write-Host ""
    Write-Host "[3/6] Building service..." -ForegroundColor Yellow
    Push-Location $servicePath
    try {
        $publishOutput = dotnet publish -c Release -o $InstallPath --self-contained false 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host $publishOutput -ForegroundColor Red
            Write-Error "Service build failed"
            exit 1
        }
        Write-Host "      Published to $InstallPath" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }

    # Copy models
    Write-Host ""
    Write-Host "[4/6] Copying models and runtime..." -ForegroundColor Yellow
    $destModelsPath = Join-Path $InstallPath "models"
    if (-not (Test-Path $destModelsPath)) {
        New-Item -ItemType Directory -Path $destModelsPath -Force | Out-Null
    }
    if (Test-Path $modelsPath) {
        Copy-Item -Path (Join-Path $modelsPath "*") -Destination $destModelsPath -Force -Recurse -ErrorAction SilentlyContinue
        Write-Host "      Models copied" -ForegroundColor Gray
    }

    # Copy Edge TPU DLL
    $edgetpuDll = Join-Path $runtimePath "edgetpu.dll"
    if (Test-Path $edgetpuDll) {
        Copy-Item -Path $edgetpuDll -Destination $InstallPath -Force
        Write-Host "      edgetpu.dll copied" -ForegroundColor Gray
    }

    # Verify tensorflowlite_c.dll
    $tfliteDll = Join-Path $InstallPath "tensorflowlite_c.dll"
    if (-not (Test-Path $tfliteDll)) {
        Write-Warning "tensorflowlite_c.dll not found! Download v2.4.1 from:"
        Write-Host "      https://github.com/ValYouW/tflite-dist/releases/tag/v2.4.1" -ForegroundColor Yellow
    }

    # Install/restart service
    Write-Host ""
    Write-Host "[5/6] Starting service..." -ForegroundColor Yellow
    $exePath = Join-Path $InstallPath "CoralBridge.Service.exe"

    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $existingService) {
        # Create service if it doesn't exist
        sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= "CoralBridge Object Detection" | Out-Null
        sc.exe description $ServiceName "Coral Edge TPU object detection service with DeepStack-compatible API" | Out-Null
        sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
        Write-Host "      Service created" -ForegroundColor Gray
    }

    # Set environment to Production so it uses the correct model path
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
    $envValue = "DOTNET_ENVIRONMENT=Production"
    $existingEnv = (Get-ItemProperty -Path $regPath -Name Environment -ErrorAction SilentlyContinue).Environment
    if ($existingEnv -notcontains $envValue) {
        $newEnv = @($envValue)
        if ($existingEnv) { $newEnv += $existingEnv }
        Set-ItemProperty -Path $regPath -Name Environment -Value $newEnv -Type MultiString
        Write-Host "      Environment set to Production" -ForegroundColor Gray
    }

    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 2

    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq "Running") {
        Write-Host "      Service started successfully" -ForegroundColor Green

        # Quick health check
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-RestMethod -Uri "http://localhost:5555/health" -TimeoutSec 5
            $tpuStatus = if ($health.using_edgetpu) { "active" } else { "inactive" }
            Write-Host "      Health: $($health.status), Edge TPU: $tpuStatus" -ForegroundColor Gray
        }
        catch {
            Write-Host "      Health check pending (service still initializing)" -ForegroundColor Gray
        }
    }
    else {
        Write-Warning "Service status: $($service.Status)"
    }
}
else {
    Write-Host ""
    Write-Host "[2-5] Skipping service deployment" -ForegroundColor Gray
}
#endregion

#region CLI Deployment
if (-not $SkipCli) {
    Write-Host ""
    Write-Host "[6/6] Building CLI tool..." -ForegroundColor Yellow

    # Create CLI install directory
    if (-not (Test-Path $CliInstallPath)) {
        New-Item -ItemType Directory -Path $CliInstallPath -Force | Out-Null
    }

    # Build CLI
    Push-Location $cliPath
    try {
        $publishOutput = dotnet publish -c Release -o $CliInstallPath --self-contained false 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host $publishOutput -ForegroundColor Red
            Write-Error "CLI build failed"
            exit 1
        }
        Write-Host "      Published to $CliInstallPath" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }

    # Add to PATH if requested
    if ($AddToPath) {
        $currentPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::User)
        if ($currentPath -notlike "*$CliInstallPath*") {
            $newPath = $currentPath + ";" + $CliInstallPath
            [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::User)
            Write-Host "      Added to user PATH" -ForegroundColor Green
            Write-Host "      (Restart terminal to use 'coralctl' from anywhere)" -ForegroundColor Yellow
        }
        else {
            Write-Host "      Already in PATH" -ForegroundColor Gray
        }
    }

    $cliExe = Join-Path $CliInstallPath "coralctl.exe"
    Write-Host "      CLI: $cliExe" -ForegroundColor Gray
}
else {
    Write-Host ""
    Write-Host "[6/6] Skipping CLI deployment" -ForegroundColor Gray
}
#endregion

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "        Deployment Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $SkipService) {
    Write-Host "Service:" -ForegroundColor White
    Write-Host "  URL:      http://localhost:5555" -ForegroundColor Gray
    Write-Host "  Path:     $InstallPath" -ForegroundColor Gray
    Write-Host ""
}

if (-not $SkipCli) {
    Write-Host "CLI Tool:" -ForegroundColor White
    $cliExePath = Join-Path $CliInstallPath "coralctl.exe"
    Write-Host "  Path:     $cliExePath" -ForegroundColor Gray
    if (-not $AddToPath) {
        Write-Host "  Tip:      Run with -AddToPath to add to PATH" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor White
    Write-Host "  coralctl stats           # View stats" -ForegroundColor Gray
    Write-Host "  coralctl stats -w        # Watch mode" -ForegroundColor Gray
    Write-Host "  coralctl stats -f        # Fahrenheit" -ForegroundColor Gray
    Write-Host "  coralctl health          # Health check" -ForegroundColor Gray
}

Write-Host ""
