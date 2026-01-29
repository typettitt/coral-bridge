namespace CoralBridge.Core;

/// <summary>
/// Interface for object detection implementations
/// </summary>
public interface IObjectDetector : IDisposable
{
    /// <summary>
    /// Gets the name/path of the loaded model
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Gets whether the Edge TPU accelerator is being used
    /// </summary>
    bool UsingEdgeTpu { get; }

    /// <summary>
    /// Gets the expected input width for the model
    /// </summary>
    int InputWidth { get; }

    /// <summary>
    /// Gets the expected input height for the model
    /// </summary>
    int InputHeight { get; }

    /// <summary>
    /// Performs object detection on an image
    /// </summary>
    /// <param name="imageData">Image data as RGB bytes in NHWC format [1, H, W, 3]</param>
    /// <param name="originalWidth">Original image width (for coordinate scaling)</param>
    /// <param name="originalHeight">Original image height (for coordinate scaling)</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold for detections</param>
    /// <returns>Detection results</returns>
    DetectionResult Detect(
        ReadOnlySpan<byte> imageData,
        int originalWidth,
        int originalHeight,
        float confidenceThreshold = 0.45f);
}
