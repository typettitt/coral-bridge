namespace CoralBridge.Core;

/// <summary>
/// Represents a single object detection result
/// </summary>
public sealed record Detection
{
    /// <summary>
    /// Class label (e.g., "person", "car")
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Detection confidence score (0.0 to 1.0)
    /// </summary>
    public required float Confidence { get; init; }

    /// <summary>
    /// Left coordinate (pixels)
    /// </summary>
    public required int XMin { get; init; }

    /// <summary>
    /// Top coordinate (pixels)
    /// </summary>
    public required int YMin { get; init; }

    /// <summary>
    /// Right coordinate (pixels)
    /// </summary>
    public required int XMax { get; init; }

    /// <summary>
    /// Bottom coordinate (pixels)
    /// </summary>
    public required int YMax { get; init; }
}

/// <summary>
/// Contains the results of an object detection operation
/// </summary>
public sealed record DetectionResult
{
    /// <summary>
    /// Whether the detection was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// List of detected objects
    /// </summary>
    public required IReadOnlyList<Detection> Predictions { get; init; }

    /// <summary>
    /// Error message if detection failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Inference time in milliseconds
    /// </summary>
    public long? InferenceTimeMs { get; init; }

    /// <summary>
    /// Creates a successful result with the given predictions
    /// </summary>
    public static DetectionResult Ok(IReadOnlyList<Detection> predictions, long? inferenceTimeMs = null) =>
        new()
        {
            Success = true,
            Predictions = predictions,
            InferenceTimeMs = inferenceTimeMs
        };

    /// <summary>
    /// Creates a failed result with the given error message
    /// </summary>
    public static DetectionResult Fail(string error) =>
        new()
        {
            Success = false,
            Predictions = [],
            Error = error
        };
}
