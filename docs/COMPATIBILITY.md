# TensorFlow Lite / Edge TPU Compatibility Notes

This document captures learnings about compatibility between TensorFlow Lite C libraries and the Coral Edge TPU runtime.

## Solution Summary

**Working Configuration:**
- `tensorflowlite_c.dll` v2.4.1 from [ValYouW/tflite-dist](https://github.com/ValYouW/tflite-dist/releases/tag/v2.4.1)
- `edgetpu.dll` from Coral Edge TPU runtime (July 2021)
- Result: **15-25ms inference time** on Coral M.2 PCIe TPU

## The Problem

When using `tensorflowlite_c.dll` (TF 2.17.1) with `edgetpu.dll` (from 2021), inference fails with:

```
ERROR: Encountered an unresolved custom op. Did you miss a custom op or delegate?
ERROR: Node number 4 (EdgeTpuDelegateForCustomOp) failed to invoke.
```

## Root Cause

**Version Mismatch**: The Edge TPU runtime (`edgetpu.dll`) was built against TensorFlow ~2.4.x in early 2021. The TensorFlow Lite C API changed significantly between TF 2.4 and TF 2.17, causing ABI incompatibility.

### Timeline
- **January 2021**: `tensorflowlite_c.dll` v2.4.1 released (ValYouW/tflite-dist)
- **July 2021**: `edgetpu.dll` released (Coral runtime)
- **December 2024**: `tensorflowlite_c.dll` v2.17.1 from tphakala/tflite_c

## Version Compatibility Matrix

| edgetpu.dll Version | TensorFlow Version | Status |
|---------------------|-------------------|--------|
| 2021-07-20 | TF 2.4.1 | **WORKS** ✓ |
| 2021-07-20 | TF 2.17.1 | FAILS - ABI mismatch |

## What We Tested

### TF 2.17.1 (FAILED)
- Source: tphakala/tflite_c
- Size: ~4.5MB
- Issues:
  - `TfLiteInterpreterOptionsAddDelegate` - delegates not applied correctly
  - `TfLiteInterpreterModifyGraphWithDelegate` - custom op fails at invoke
  - ABI incompatibility with edgetpu.dll delegate execution

### TF 2.4.1 (SUCCESS)
- Source: [ValYouW/tflite-dist v2.4.1](https://github.com/ValYouW/tflite-dist/releases/tag/v2.4.1)
- Size: ~1.6MB
- Download: `tflite-dist.zip` → `tflite-dist/libs/windows_x86_64/tensorflowlite_c.dll`
- Result: Full Edge TPU acceleration working

## Edge TPU Runtime Details

From runtime logs:
```
Edge TPU runtime version: BuildLabel(COMPILER=MSVC 192528612,DATE=Jul 20 2021,TIME=14:30:22), RuntimeVersion(14)
Found 1 Edge TPU device(s)
  - Type: ApexPci, Path: \\?\ApexDevice0
Edge TPU delegate created and added to options (device type: ApexPci)
```

## Performance Results

With TF 2.4.1 + Edge TPU:
```
Inference completed in 23ms
Average latency: 15.4ms (over 5 requests)
Min: 14.6ms, Max: 16.2ms
```

## API Notes

### TF 2.4.x API (what works)
```csharp
// Add delegate to options BEFORE creating interpreter
options.AddDelegate(edgeTpuDelegate);
var interpreter = TfLiteInterpreterCreate(model, options);
interpreter.AllocateTensors();
interpreter.Invoke();  // SUCCESS
```

### TF 2.17.x API (not compatible with 2021 edgetpu.dll)
```csharp
// ModifyGraphWithDelegate exists but delegate execution fails
var interpreter = TfLiteInterpreterCreate(model, options);
TfLiteInterpreterModifyGraphWithDelegate(interpreter, edgeTpuDelegate);
interpreter.AllocateTensors();
interpreter.Invoke();  // FAILS: custom op unresolved
```

## Future Considerations

If you need newer TensorFlow features, you would need to:
1. Fork tphakala/tflite_c and build against TF 2.4.x/2.5.x
2. Wait for Google to update the Edge TPU runtime (unlikely)
3. Use Python/pycoral instead (different architecture)

## References

- [ValYouW/tflite-dist](https://github.com/ValYouW/tflite-dist) - **Recommended** TF Lite distribution with v2.4.1
- [tphakala/tflite_c](https://github.com/tphakala/tflite_c) - Modern builds (incompatible with 2021 edgetpu.dll)
- [Coral Edge TPU Runtime](https://coral.ai/docs/accelerator/get-started/) - Official Coral documentation
- [TensorFlow Lite C API](https://www.tensorflow.org/lite/guide/inference#load_and_run_a_model_in_c) - Official TF Lite C docs
