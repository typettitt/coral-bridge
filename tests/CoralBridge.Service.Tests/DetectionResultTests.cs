using CoralBridge.Core;
using Xunit;

namespace CoralBridge.Service.Tests;

public class DetectionResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        var predictions = new List<Detection>
        {
            new() { Label = "person", Confidence = 0.95f, XMin = 10, YMin = 20, XMax = 100, YMax = 200 }
        };

        var result = DetectionResult.Ok(predictions, inferenceTimeMs: 50);

        Assert.True(result.Success);
        Assert.Single(result.Predictions);
        Assert.Null(result.Error);
        Assert.Equal(50, result.InferenceTimeMs);
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        var result = DetectionResult.Fail("Something went wrong");

        Assert.False(result.Success);
        Assert.Empty(result.Predictions);
        Assert.Equal("Something went wrong", result.Error);
        Assert.Null(result.InferenceTimeMs);
    }

    [Fact]
    public void Detection_RecordEquality()
    {
        var d1 = new Detection
        {
            Label = "person",
            Confidence = 0.95f,
            XMin = 10,
            YMin = 20,
            XMax = 100,
            YMax = 200
        };

        var d2 = new Detection
        {
            Label = "person",
            Confidence = 0.95f,
            XMin = 10,
            YMin = 20,
            XMax = 100,
            YMax = 200
        };

        Assert.Equal(d1, d2);
    }
}
