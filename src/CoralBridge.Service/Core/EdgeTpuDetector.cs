using System.Diagnostics;
using CoralBridge.Native;
using CoralBridge.Native.SafeHandles;
using CoralBridge.Processing;
using Microsoft.Extensions.Logging;

namespace CoralBridge.Core;

/// <summary>
/// Object detector using TensorFlow Lite with Edge TPU acceleration
/// </summary>
public sealed class EdgeTpuDetector : IObjectDetector
{
    private readonly ILogger<EdgeTpuDetector> _logger;
    private readonly TfLiteModelHandle _model;
    private readonly TfLiteOptionsHandle _options;
    private readonly TfLiteInterpreterHandle _interpreter;
    private readonly EdgeTpuDelegateHandle? _edgeTpuDelegate;
    private readonly object _inferenceLock = new();
    private bool _disposed;

    public string ModelName { get; }
    public bool UsingEdgeTpu { get; }
    public int InputWidth { get; } = 300;
    public int InputHeight { get; } = 300;

    public EdgeTpuDetector(string modelPath, ILogger<EdgeTpuDetector> logger)
    {
        _logger = logger;
        ModelName = Path.GetFileName(modelPath);

        // Initialize native library resolver
        NativeLibraryLoader.Initialize();

        _logger.LogInformation("Loading model from: {ModelPath}", modelPath);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Model file not found", modelPath);
        }

        // Load the model
        _model = TfLiteModelHandle.CreateFromFile(modelPath);
        _logger.LogInformation("Model loaded successfully");

        // Create interpreter options
        _options = TfLiteOptionsHandle.Create();
        _options.SetNumThreads(Environment.ProcessorCount);

        // Try to create Edge TPU delegate
        try
        {
            _logger.LogInformation("Attempting to initialize Edge TPU...");

            // Log Edge TPU version
            var version = EdgeTpuInterop.GetVersion();
            _logger.LogInformation("Edge TPU runtime version: {Version}", version ?? "unknown");

            // List available devices
            var devices = EdgeTpuInterop.ListDevices();
            _logger.LogInformation("Found {Count} Edge TPU device(s)", devices.Length);

            foreach (var device in devices)
            {
                _logger.LogInformation("  - Type: {Type}, Path: {Path}",
                    device.Type,
                    device.GetPath() ?? "unknown");
            }

            // Create delegate for any available device
            var (delegateHandle, deviceType) = EdgeTpuDelegateHandle.CreateForAnyDevice();
            _edgeTpuDelegate = delegateHandle;

            if (_edgeTpuDelegate != null)
            {
                _options.AddDelegate(_edgeTpuDelegate.DangerousGetHandle());
                UsingEdgeTpu = true;
                _logger.LogInformation("Edge TPU delegate created and added to options (device type: {DeviceType})", deviceType);
            }
            else
            {
                _logger.LogWarning("Failed to create Edge TPU delegate, falling back to CPU");
            }
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex, "Edge TPU library not found, falling back to CPU inference");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Edge TPU, falling back to CPU inference");
        }

        // Create the interpreter with delegate in options
        _interpreter = TfLiteInterpreterHandle.Create(_model, _options);
        _logger.LogInformation("Interpreter created successfully");

        // Allocate tensors
        _interpreter.AllocateTensors();
        _logger.LogInformation("Tensors allocated");

        // Log input tensor info
        var inputCount = _interpreter.GetInputTensorCount();
        _logger.LogInformation("Input tensor count: {Count}", inputCount);

        if (inputCount > 0)
        {
            var inputTensor = _interpreter.GetInputTensor(0);
            var dims = _interpreter.GetTensorDimensions(inputTensor);
            _logger.LogInformation("Input tensor shape: [{Dims}]", string.Join(", ", dims));

            // Update input dimensions if different from default
            if (dims.Length >= 3)
            {
                InputHeight = dims[1];
                InputWidth = dims[2];
            }
        }

        // Log output tensor info
        var outputCount = _interpreter.GetOutputTensorCount();
        _logger.LogInformation("Output tensor count: {Count}", outputCount);

        for (var i = 0; i < outputCount; i++)
        {
            var outputTensor = _interpreter.GetOutputTensor(i);
            var dims = _interpreter.GetTensorDimensions(outputTensor);
            var type = TfLiteInterop.TfLiteTensorType(outputTensor);
            _logger.LogDebug("Output tensor {Index} shape: [{Dims}], type: {Type}",
                i, string.Join(", ", dims), type);
        }

        _logger.LogInformation("Detector initialized - Using Edge TPU: {UsingEdgeTpu}", UsingEdgeTpu);
    }

    public DetectionResult Detect(
        ReadOnlySpan<byte> imageData,
        int originalWidth,
        int originalHeight,
        float confidenceThreshold = 0.45f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var expectedSize = InputWidth * InputHeight * 3;
        if (imageData.Length != expectedSize)
        {
            return DetectionResult.Fail(
                $"Invalid input size. Expected {expectedSize} bytes, got {imageData.Length}");
        }

        try
        {
            long inferenceTimeMs;

            // Synchronize inference (TFLite interpreter is not thread-safe)
            lock (_inferenceLock)
            {
                // Copy input data to tensor
                _interpreter.CopyToInputTensor(0, imageData);

                // Run inference
                var sw = Stopwatch.StartNew();
                _interpreter.Invoke();
                sw.Stop();
                inferenceTimeMs = sw.ElapsedMilliseconds;

                _logger.LogDebug("Inference completed in {Ms}ms", inferenceTimeMs);
            }

            // Parse output tensors
            // SSD MobileNet post-processed model outputs:
            // 0: Boxes [1, N, 4] - ymin, xmin, ymax, xmax (normalized 0-1)
            // 1: Classes [1, N] - class indices
            // 2: Scores [1, N] - confidence scores
            // 3: Count [1] - number of detections

            var boxes = _interpreter.GetOutputTensorDataAsFloat(0);
            var classes = _interpreter.GetOutputTensorDataAsFloat(1);
            var scores = _interpreter.GetOutputTensorDataAsFloat(2);
            var countData = _interpreter.GetOutputTensorDataAsFloat(3);

            var count = (int)countData[0];
            var detections = new List<Detection>();

            for (var i = 0; i < count; i++)
            {
                var score = scores[i];
                if (score < confidenceThreshold)
                {
                    continue;
                }

                var classId = (int)classes[i];
                var label = CocoLabels.GetLabel(classId);

                // Box format: ymin, xmin, ymax, xmax (normalized)
                var ymin = boxes[i * 4 + 0];
                var xmin = boxes[i * 4 + 1];
                var ymax = boxes[i * 4 + 2];
                var xmax = boxes[i * 4 + 3];

                // Scale to original image dimensions
                detections.Add(new Detection
                {
                    Label = label,
                    Confidence = score,
                    XMin = (int)(xmin * originalWidth),
                    YMin = (int)(ymin * originalHeight),
                    XMax = (int)(xmax * originalWidth),
                    YMax = (int)(ymax * originalHeight)
                });
            }

            _logger.LogDebug("Found {Count} detection(s) above threshold {Threshold}",
                detections.Count, confidenceThreshold);

            return DetectionResult.Ok(detections, inferenceTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detection failed");
            return DetectionResult.Fail($"Detection failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _interpreter.Dispose();
        _edgeTpuDelegate?.Dispose();
        _options.Dispose();
        _model.Dispose();

        _logger.LogInformation("Detector disposed");
    }
}
