using System.Text.Json.Serialization;
using CoralBridge.Core;
using CoralBridge.Processing;

namespace CoralBridge.Api;

/// <summary>
/// API endpoint handlers for DeepStack-compatible object detection
/// </summary>
public static class Endpoints
{
    /// <summary>
    /// Maps all API endpoints to the application
    /// </summary>
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapGet("/", HandleRoot);
        app.MapGet("/health", HandleHealth);
        app.MapPost("/v1/vision/detection", HandleDetection);
    }

    /// <summary>
    /// Root endpoint - returns service info
    /// </summary>
    private static IResult HandleRoot()
    {
        return Results.Ok(new ServiceInfo
        {
            Name = "CoralBridge",
            Version = "1.0.0",
            Status = "running"
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    private static IResult HandleHealth(IObjectDetector detector)
    {
        return Results.Ok(new HealthResponse
        {
            Status = "healthy",
            Model = detector.ModelName,
            UsingEdgeTpu = detector.UsingEdgeTpu,
            InputWidth = detector.InputWidth,
            InputHeight = detector.InputHeight
        });
    }

    /// <summary>
    /// Object detection endpoint - DeepStack compatible
    /// </summary>
    private static async Task<IResult> HandleDetection(
        HttpRequest request,
        IObjectDetector detector,
        ILogger<Program> logger)
    {
        try
        {
            // Extract confidence threshold from form if provided
            var confidenceThreshold = 0.45f;
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync();
                if (form.TryGetValue("min_confidence", out var minConfStr) &&
                    float.TryParse(minConfStr, out var minConf))
                {
                    confidenceThreshold = minConf;
                }
            }

            // Extract image from request
            var imageResult = await ImagePreprocessor.ExtractImageFromFormAsync(request);
            if (imageResult == null)
            {
                return Results.BadRequest(new DeepStackResponse
                {
                    Success = false,
                    Error = "No image provided. Send image as multipart form data with field name 'image'."
                });
            }

            var (imageStream, fileName) = imageResult.Value;
            logger.LogDebug("Processing image: {FileName}", fileName ?? "unknown");

            // Preprocess the image
            var preprocessed = await ImagePreprocessor.PreprocessAsync(
                imageStream,
                detector.InputWidth,
                detector.InputHeight);

            logger.LogDebug("Image preprocessed: original {W}x{H}",
                preprocessed.OriginalWidth, preprocessed.OriginalHeight);

            // Run detection
            var result = detector.Detect(
                preprocessed.Data,
                preprocessed.OriginalWidth,
                preprocessed.OriginalHeight,
                confidenceThreshold);

            if (!result.Success)
            {
                return Results.Ok(new DeepStackResponse
                {
                    Success = false,
                    Error = result.Error
                });
            }

            // Convert to DeepStack format
            var predictions = result.Predictions.Select(d => new DeepStackPrediction
            {
                Label = d.Label,
                Confidence = d.Confidence,
                XMin = d.XMin,
                YMin = d.YMin,
                XMax = d.XMax,
                YMax = d.YMax
            }).ToList();

            logger.LogInformation("Detection completed: {Count} objects found in {Ms}ms",
                predictions.Count, result.InferenceTimeMs);

            return Results.Ok(new DeepStackResponse
            {
                Success = true,
                Predictions = predictions
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Detection request failed");
            return Results.Ok(new DeepStackResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}

#region Response Models

/// <summary>
/// Service information response
/// </summary>
public record ServiceInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}

/// <summary>
/// Health check response
/// </summary>
public record HealthResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("using_edgetpu")]
    public required bool UsingEdgeTpu { get; init; }

    [JsonPropertyName("input_width")]
    public required int InputWidth { get; init; }

    [JsonPropertyName("input_height")]
    public required int InputHeight { get; init; }
}

/// <summary>
/// DeepStack-compatible detection response
/// </summary>
public record DeepStackResponse
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("predictions")]
    public List<DeepStackPrediction>? Predictions { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// DeepStack-compatible prediction format
/// </summary>
public record DeepStackPrediction
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("confidence")]
    public required float Confidence { get; init; }

    [JsonPropertyName("x_min")]
    public required int XMin { get; init; }

    [JsonPropertyName("y_min")]
    public required int YMin { get; init; }

    [JsonPropertyName("x_max")]
    public required int XMax { get; init; }

    [JsonPropertyName("y_max")]
    public required int YMax { get; init; }
}

#endregion
