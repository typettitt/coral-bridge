using CoralBridge.Core;
using CoralBridge.Processing;
using Xunit;

namespace CoralBridge.Service.Tests;

public class TensorOutputParserTests
{
    [Fact]
    public void ParseDetections_EmptyInput_ReturnsEmptyList()
    {
        var boxes = Array.Empty<float>();
        var classes = Array.Empty<float>();
        var scores = Array.Empty<float>();

        var result = TensorOutputParser.ParseDetections(
            boxes, classes, scores, count: 0,
            originalWidth: 640, originalHeight: 480,
            confidenceThreshold: 0.5f);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseDetections_SingleDetection_ReturnsCorrectResult()
    {
        // Box format: ymin, xmin, ymax, xmax (normalized)
        var boxes = new float[] { 0.1f, 0.2f, 0.5f, 0.8f };
        var classes = new float[] { 0f }; // person
        var scores = new float[] { 0.95f };

        var result = TensorOutputParser.ParseDetections(
            boxes, classes, scores, count: 1,
            originalWidth: 100, originalHeight: 100,
            confidenceThreshold: 0.5f);

        Assert.Single(result);

        var detection = result[0];
        Assert.Equal("person", detection.Label);
        Assert.Equal(0.95f, detection.Confidence);
        Assert.Equal(20, detection.XMin);  // 0.2 * 100
        Assert.Equal(10, detection.YMin);  // 0.1 * 100
        Assert.Equal(80, detection.XMax);  // 0.8 * 100
        Assert.Equal(50, detection.YMax);  // 0.5 * 100
    }

    [Fact]
    public void ParseDetections_BelowThreshold_ReturnsEmpty()
    {
        var boxes = new float[] { 0.1f, 0.2f, 0.5f, 0.8f };
        var classes = new float[] { 0f };
        var scores = new float[] { 0.3f }; // Below 0.5 threshold

        var result = TensorOutputParser.ParseDetections(
            boxes, classes, scores, count: 1,
            originalWidth: 100, originalHeight: 100,
            confidenceThreshold: 0.5f);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseDetections_MultipleDetections_SortedByConfidence()
    {
        var boxes = new float[]
        {
            0.1f, 0.1f, 0.2f, 0.2f,
            0.3f, 0.3f, 0.4f, 0.4f,
            0.5f, 0.5f, 0.6f, 0.6f
        };
        var classes = new float[] { 0f, 2f, 16f }; // person, car, cat
        var scores = new float[] { 0.6f, 0.9f, 0.75f };

        var result = TensorOutputParser.ParseDetections(
            boxes, classes, scores, count: 3,
            originalWidth: 100, originalHeight: 100,
            confidenceThreshold: 0.5f);

        Assert.Equal(3, result.Count);
        Assert.Equal("car", result[0].Label);     // 0.9 - highest
        Assert.Equal("cat", result[1].Label);     // 0.75
        Assert.Equal("person", result[2].Label);  // 0.6 - lowest
    }

    [Fact]
    public void ApplyNms_NoOverlap_KeepsAll()
    {
        var detections = new List<Detection>
        {
            new() { Label = "person", Confidence = 0.9f, XMin = 0, YMin = 0, XMax = 10, YMax = 10 },
            new() { Label = "person", Confidence = 0.8f, XMin = 100, YMin = 100, XMax = 110, YMax = 110 }
        };

        var result = TensorOutputParser.ApplyNms(detections, iouThreshold: 0.5f);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyNms_HighOverlap_SuppressesLowerConfidence()
    {
        var detections = new List<Detection>
        {
            new() { Label = "person", Confidence = 0.9f, XMin = 0, YMin = 0, XMax = 100, YMax = 100 },
            new() { Label = "person", Confidence = 0.8f, XMin = 10, YMin = 10, XMax = 110, YMax = 110 } // High overlap
        };

        var result = TensorOutputParser.ApplyNms(detections, iouThreshold: 0.5f);

        Assert.Single(result);
        Assert.Equal(0.9f, result[0].Confidence);
    }

    [Fact]
    public void ApplyNms_DifferentClasses_KeepsBoth()
    {
        var detections = new List<Detection>
        {
            new() { Label = "person", Confidence = 0.9f, XMin = 0, YMin = 0, XMax = 100, YMax = 100 },
            new() { Label = "car", Confidence = 0.8f, XMin = 10, YMin = 10, XMax = 110, YMax = 110 }
        };

        var result = TensorOutputParser.ApplyNms(detections, iouThreshold: 0.5f);

        // Different classes should not suppress each other
        Assert.Equal(2, result.Count);
    }
}
