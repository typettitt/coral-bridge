using CoralBridge.Processing;
using Xunit;

namespace CoralBridge.Service.Tests;

public class CocoLabelsTests
{
    [Theory]
    [InlineData(0, "person")]
    [InlineData(1, "bicycle")]
    [InlineData(2, "car")]
    [InlineData(16, "cat")]
    [InlineData(17, "dog")]
    public void GetLabel_KnownClass_ReturnsCorrectLabel(int classId, string expectedLabel)
    {
        var result = CocoLabels.GetLabel(classId);
        Assert.Equal(expectedLabel, result);
    }

    [Fact]
    public void GetLabel_UnknownClass_ReturnsClassId()
    {
        var result = CocoLabels.GetLabel(999);
        Assert.Equal("class_999", result);
    }

    [Fact]
    public void All_ContainsExpectedNumberOfLabels()
    {
        // COCO has 80 actual object classes
        Assert.True(CocoLabels.All.Count >= 80);
    }
}
