<#
.SYNOPSIS
    Tests the CoralBridge detection API

.DESCRIPTION
    Sends a test image to the CoralBridge API and displays the detection results.

.PARAMETER ImagePath
    Path to the image file to test (required)

.PARAMETER Url
    Base URL of the CoralBridge service (default: http://localhost:5555)

.PARAMETER MinConfidence
    Minimum confidence threshold (default: 0.45)

.EXAMPLE
    .\test-detection.ps1 -ImagePath .\test.jpg

.EXAMPLE
    .\test-detection.ps1 -ImagePath .\test.jpg -MinConfidence 0.7
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [string]$Url = "http://localhost:5555",

    [float]$MinConfidence = 0.45
)

$ErrorActionPreference = "Stop"

# Check if image exists
if (-not (Test-Path $ImagePath)) {
    Write-Error "Image file not found: $ImagePath"
    exit 1
}

$imagePath = Resolve-Path $ImagePath

Write-Host "=== CoralBridge Detection Test ===" -ForegroundColor Cyan
Write-Host ""

# Test health endpoint first
Write-Host "Checking service health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$Url/health" -Method Get
    Write-Host "  Status: $($health.status)" -ForegroundColor Green
    Write-Host "  Model: $($health.model)" -ForegroundColor Gray
    Write-Host "  Using Edge TPU: $($health.using_edgetpu)" -ForegroundColor Gray
}
catch {
    Write-Error "Failed to connect to service at $Url. Is the service running?"
    exit 1
}

Write-Host ""
Write-Host "Sending image for detection..." -ForegroundColor Yellow
Write-Host "  Image: $imagePath" -ForegroundColor Gray
Write-Host "  Min Confidence: $MinConfidence" -ForegroundColor Gray

# Send detection request
try {
    $boundary = [System.Guid]::NewGuid().ToString()
    $fileBytes = [System.IO.File]::ReadAllBytes($imagePath)
    $fileName = [System.IO.Path]::GetFileName($imagePath)

    $bodyLines = @(
        "--$boundary",
        "Content-Disposition: form-data; name=`"image`"; filename=`"$fileName`"",
        "Content-Type: application/octet-stream",
        "",
        [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes),
        "--$boundary",
        "Content-Disposition: form-data; name=`"min_confidence`"",
        "",
        $MinConfidence.ToString(),
        "--$boundary--"
    )

    $body = $bodyLines -join "`r`n"

    $startTime = Get-Date
    $response = Invoke-RestMethod -Uri "$Url/v1/vision/detection" `
        -Method Post `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Body $body
    $elapsed = (Get-Date) - $startTime

    Write-Host ""
    Write-Host "Response received in $([int]$elapsed.TotalMilliseconds)ms" -ForegroundColor Green
    Write-Host ""

    if ($response.success) {
        $predictions = $response.predictions
        if ($predictions -and $predictions.Count -gt 0) {
            Write-Host "Detections found: $($predictions.Count)" -ForegroundColor Cyan
            Write-Host ""

            foreach ($pred in $predictions) {
                $conf = [math]::Round($pred.confidence * 100, 1)
                Write-Host "  [$conf%] $($pred.label)" -ForegroundColor White
                Write-Host "         Box: ($($pred.x_min), $($pred.y_min)) to ($($pred.x_max), $($pred.y_max))" -ForegroundColor Gray
            }
        }
        else {
            Write-Host "No objects detected above confidence threshold." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Detection failed: $($response.error)" -ForegroundColor Red
    }
}
catch {
    Write-Error "Request failed: $_"
    exit 1
}

Write-Host ""
