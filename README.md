```
 ██████╗ ██████╗ ██████╗  █████╗ ██╗         ██████╗ ██████╗ ██╗██████╗  ██████╗ ███████╗
██╔════╝██╔═══██╗██╔══██╗██╔══██╗██║         ██╔══██╗██╔══██╗██║██╔══██╗██╔════╝ ██╔════╝
██║     ██║   ██║██████╔╝███████║██║         ██████╔╝██████╔╝██║██║  ██║██║  ███╗█████╗
██║     ██║   ██║██╔══██╗██╔══██║██║         ██╔══██╗██╔══██╗██║██║  ██║██║   ██║██╔══╝
╚██████╗╚██████╔╝██║  ██║██║  ██║███████╗    ██████╔╝██║  ██║██║██████╔╝╚██████╔╝███████╗
 ╚═════╝ ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚══════╝    ╚═════╝ ╚═╝  ╚═╝╚═╝╚═════╝  ╚═════╝ ╚══════╝
```

**Use your Coral M.2 Edge TPU with Frigate on Windows.**

A .NET 10 Windows Service that bridges Docker containers to a Coral M.2 PCIe Edge TPU, exposing a DeepStack-compatible HTTP API. Built for the [Frigate NVR](https://frigate.video/) community.

## Why CoralBridge?

Docker on Windows cannot directly access PCIe hardware like the Coral M.2 Edge TPU. CoralBridge solves this by running as a native Windows service that:

1. Communicates directly with the TPU via the Edge TPU runtime
2. Exposes a DeepStack-compatible REST API on port 5555
3. Allows Frigate (or any container) to use the TPU over HTTP

```
┌─────────────────────┐     HTTP (port 5555)     ┌──────────────────────┐
│  Frigate (Docker)   │ ──────────────────────▶  │  CoralBridge Service │
│                     │     /v1/vision/detection │  (.NET 10 Windows)   │
└─────────────────────┘                          └──────────┬───────────┘
                                                            │ P/Invoke
                                                            ▼
                                                 ┌──────────────────────┐
                                                 │  Coral M.2 TPU       │
                                                 │  (~15ms inference)   │
                                                 └──────────────────────┘
```

## Performance

With Edge TPU acceleration:
- **15-25ms** average inference time
- Handles multiple concurrent requests
- Runs as a lightweight Windows service

## Requirements

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Coral M.2 Edge TPU (PCIe) with drivers installed

## Quick Start

### 1. Clone and Setup

```powershell
git clone https://github.com/typettitt/coral-bridge.git
cd coral-bridge
```

### 2. Install Edge TPU Runtime

Run as Administrator:
```powershell
.\runtime\edgetpu_runtime\install.bat
```

### 3. Download TensorFlow Lite Library

Download `tensorflowlite_c.dll` v2.4.1 from [ValYouW/tflite-dist](https://github.com/ValYouW/tflite-dist/releases/tag/v2.4.1):

1. Download `tflite-dist.zip` from the release
2. Extract `tflite-dist/libs/windows_x86_64/tensorflowlite_c.dll`
3. Place in the `runtime/` folder

> **Important:** You must use TensorFlow Lite **v2.4.1** specifically. Newer versions are incompatible with the Edge TPU runtime. See [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md) for details.

### 4. Download a Model

Download an Edge TPU model to the `models/` folder. Recommended:
- [ssd_mobilenet_v1_coco_quant_postprocess_edgetpu.tflite](https://github.com/google-coral/test_data/raw/master/ssd_mobilenet_v1_coco_quant_postprocess_edgetpu.tflite)

```powershell
Invoke-WebRequest -Uri "https://github.com/google-coral/test_data/raw/master/ssd_mobilenet_v1_coco_quant_postprocess_edgetpu.tflite" -OutFile "models/ssd_mobilenet_v1_coco_quant_postprocess_edgetpu.tflite"
```

### 5. Run the Service

```powershell
cd src/CoralBridge.Service
dotnet run
```

### 6. Test It

```powershell
# Health check
curl http://localhost:5555/health

# Should show: "using_edgetpu": true
```

## Install as Windows Service

To run CoralBridge automatically on startup:

```powershell
# Run as Administrator
.\scripts\install-service.ps1
```

## Frigate Configuration

Configure Frigate to use CoralBridge as a detector in your `frigate.yml`:

```yaml
detectors:
  coralbridge:
    type: deepstack
    api_url: http://host.docker.internal:5555/v1/vision/detection
```

## API Reference

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Service info |
| GET | `/health` | Status, model name, TPU availability |
| POST | `/v1/vision/detection` | Object detection (DeepStack format) |

### Detection Request

```bash
curl -X POST http://localhost:5555/v1/vision/detection \
  -F "image=@photo.jpg" \
  -F "min_confidence=0.5"
```

### Detection Response

```json
{
  "success": true,
  "predictions": [
    {
      "label": "person",
      "confidence": 0.95,
      "x_min": 100,
      "y_min": 150,
      "x_max": 300,
      "y_max": 500
    }
  ]
}
```

## Configuration

Edit `src/CoralBridge.Service/appsettings.json`:

```json
{
  "CoralBridge": {
    "ModelPath": "../../models/ssd_mobilenet_v1_coco_quant_postprocess_edgetpu.tflite",
    "DefaultConfidence": 0.45
  },
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://0.0.0.0:5555" }
    }
  }
}
```

## Building from Source

```powershell
# Build
dotnet build

# Run tests
dotnet test

# Publish release
dotnet publish src/CoralBridge.Service -c Release -o publish
```

## Troubleshooting

### Service won't start
1. Check Windows Event Viewer for errors
2. Verify Edge TPU drivers installed (`runtime/edgetpu_runtime/install.bat`)
3. Ensure `tensorflowlite_c.dll` v2.4.1 is in `runtime/`
4. Check port 5555 is not in use

### No Edge TPU detected
1. Verify TPU appears in Device Manager under "Coral PCIe Accelerator"
2. Re-run `install.bat` as Administrator
3. Check `/health` endpoint - should show `using_edgetpu: true`

### Slow inference (>100ms)
- Verify `using_edgetpu: true` in health response
- Ensure using an `_edgetpu.tflite` model variant
- Check CPU isn't maxed out

## Project Structure

```
coral-bridge/
├── src/CoralBridge.Service/    # Main service
│   ├── Native/                 # P/Invoke bindings for TFLite & EdgeTPU
│   ├── Core/                   # Detection engine
│   ├── Processing/             # Image preprocessing
│   └── Api/                    # REST endpoints
├── tests/                      # Unit tests
├── models/                     # TFLite models (download separately)
├── runtime/                    # Native DLLs
│   ├── tensorflowlite_c.dll    # TFLite v2.4.1 (download separately)
│   └── edgetpu_runtime/        # Edge TPU installer & DLLs
├── scripts/                    # Install/test scripts
└── docs/                       # Additional documentation
```

## License

MIT

## Acknowledgments

- [Google Coral](https://coral.ai/) for the Edge TPU hardware and runtime
- [Frigate NVR](https://frigate.video/) for the inspiration
- [ValYouW/tflite-dist](https://github.com/ValYouW/tflite-dist) for compatible TFLite builds
