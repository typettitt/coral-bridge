using CoralBridge.Core;

namespace CoralBridge.Processing;

/// <summary>
/// Parses output tensors from SSD MobileNet models
/// </summary>
public static class TensorOutputParser
{
    /// <summary>
    /// Parses detections from SSD MobileNet output tensors.
    ///
    /// Post-processed SSD MobileNet models output 4 tensors:
    /// - Tensor 0: Boxes [1, N, 4] - ymin, xmin, ymax, xmax (normalized 0-1)
    /// - Tensor 1: Classes [1, N] - class indices (float, need to cast to int)
    /// - Tensor 2: Scores [1, N] - confidence scores (0-1)
    /// - Tensor 3: Count [1] - number of valid detections (float, need to cast to int)
    /// </summary>
    /// <param name="boxes">Bounding boxes tensor data</param>
    /// <param name="classes">Class indices tensor data</param>
    /// <param name="scores">Confidence scores tensor data</param>
    /// <param name="count">Number of detections</param>
    /// <param name="originalWidth">Original image width for coordinate scaling</param>
    /// <param name="originalHeight">Original image height for coordinate scaling</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold</param>
    /// <returns>List of detections above the confidence threshold</returns>
    public static List<Detection> ParseDetections(
        ReadOnlySpan<float> boxes,
        ReadOnlySpan<float> classes,
        ReadOnlySpan<float> scores,
        int count,
        int originalWidth,
        int originalHeight,
        float confidenceThreshold)
    {
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

            // Box format: ymin, xmin, ymax, xmax (normalized 0-1)
            var ymin = Math.Clamp(boxes[i * 4 + 0], 0f, 1f);
            var xmin = Math.Clamp(boxes[i * 4 + 1], 0f, 1f);
            var ymax = Math.Clamp(boxes[i * 4 + 2], 0f, 1f);
            var xmax = Math.Clamp(boxes[i * 4 + 3], 0f, 1f);

            // Scale to original image dimensions
            var scaledXMin = (int)(xmin * originalWidth);
            var scaledYMin = (int)(ymin * originalHeight);
            var scaledXMax = (int)(xmax * originalWidth);
            var scaledYMax = (int)(ymax * originalHeight);

            // Ensure coordinates are valid
            if (scaledXMax <= scaledXMin || scaledYMax <= scaledYMin)
            {
                continue;
            }

            detections.Add(new Detection
            {
                Label = label,
                Confidence = score,
                XMin = scaledXMin,
                YMin = scaledYMin,
                XMax = scaledXMax,
                YMax = scaledYMax
            });
        }

        // Sort by confidence descending
        detections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

        return detections;
    }

    /// <summary>
    /// Applies Non-Maximum Suppression to filter overlapping detections.
    /// </summary>
    /// <param name="detections">List of detections</param>
    /// <param name="iouThreshold">IoU threshold for suppression (default 0.5)</param>
    /// <returns>Filtered list of detections</returns>
    public static List<Detection> ApplyNms(
        IReadOnlyList<Detection> detections,
        float iouThreshold = 0.5f)
    {
        if (detections.Count == 0)
        {
            return [];
        }

        // Sort by confidence (should already be sorted, but ensure)
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();
        var keep = new List<Detection>();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            keep.Add(best);
            sorted.RemoveAt(0);

            sorted.RemoveAll(other =>
                other.Label == best.Label && // Only suppress same class
                ComputeIou(best, other) > iouThreshold);
        }

        return keep;
    }

    /// <summary>
    /// Computes Intersection over Union (IoU) between two detections.
    /// </summary>
    private static float ComputeIou(Detection a, Detection b)
    {
        var intersectionXMin = Math.Max(a.XMin, b.XMin);
        var intersectionYMin = Math.Max(a.YMin, b.YMin);
        var intersectionXMax = Math.Min(a.XMax, b.XMax);
        var intersectionYMax = Math.Min(a.YMax, b.YMax);

        var intersectionWidth = Math.Max(0, intersectionXMax - intersectionXMin);
        var intersectionHeight = Math.Max(0, intersectionYMax - intersectionYMin);
        var intersectionArea = intersectionWidth * intersectionHeight;

        var aArea = (a.XMax - a.XMin) * (a.YMax - a.YMin);
        var bArea = (b.XMax - b.XMin) * (b.YMax - b.YMin);
        var unionArea = aArea + bArea - intersectionArea;

        return unionArea > 0 ? (float)intersectionArea / unionArea : 0;
    }
}
