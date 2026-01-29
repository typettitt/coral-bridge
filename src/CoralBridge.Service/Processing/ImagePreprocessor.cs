using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CoralBridge.Processing;

/// <summary>
/// Handles image preprocessing for the SSD MobileNet model
/// </summary>
public static class ImagePreprocessor
{
    /// <summary>
    /// Result of image preprocessing
    /// </summary>
    public readonly record struct PreprocessedImage(
        byte[] Data,
        int OriginalWidth,
        int OriginalHeight);

    /// <summary>
    /// Preprocesses an image for object detection.
    /// Resizes to the target dimensions and converts to RGB byte array.
    /// </summary>
    /// <param name="imageStream">Stream containing the image data</param>
    /// <param name="targetWidth">Target width (e.g., 300 for SSD MobileNet)</param>
    /// <param name="targetHeight">Target height (e.g., 300 for SSD MobileNet)</param>
    /// <returns>Preprocessed image data in NHWC format</returns>
    public static async Task<PreprocessedImage> PreprocessAsync(
        Stream imageStream,
        int targetWidth,
        int targetHeight)
    {
        using var image = await Image.LoadAsync<Rgb24>(imageStream);
        return Preprocess(image, targetWidth, targetHeight);
    }

    /// <summary>
    /// Preprocesses an image from a byte array.
    /// </summary>
    public static PreprocessedImage Preprocess(
        ReadOnlySpan<byte> imageBytes,
        int targetWidth,
        int targetHeight)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        return Preprocess(image, targetWidth, targetHeight);
    }

    /// <summary>
    /// Preprocesses an already-loaded image.
    /// </summary>
    public static PreprocessedImage Preprocess(
        Image<Rgb24> image,
        int targetWidth,
        int targetHeight)
    {
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Resize the image to target dimensions
        if (image.Width != targetWidth || image.Height != targetHeight)
        {
            image.Mutate(x => x.Resize(targetWidth, targetHeight));
        }

        // Convert to RGB byte array in NHWC format [1, H, W, 3]
        var data = new byte[targetWidth * targetHeight * 3];

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var baseIndex = y * targetWidth * 3;

                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    var index = baseIndex + x * 3;

                    data[index] = pixel.R;
                    data[index + 1] = pixel.G;
                    data[index + 2] = pixel.B;
                }
            }
        });

        return new PreprocessedImage(data, originalWidth, originalHeight);
    }

    /// <summary>
    /// Extracts the image from a multipart form data request.
    /// </summary>
    public static async Task<(Stream ImageStream, string? FileName)?> ExtractImageFromFormAsync(
        HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return null;
        }

        var form = await request.ReadFormAsync();

        // Check for "image" field (DeepStack format)
        if (form.Files.GetFile("image") is { } imageFile)
        {
            return (imageFile.OpenReadStream(), imageFile.FileName);
        }

        // Check for any file
        if (form.Files.Count > 0)
        {
            var file = form.Files[0];
            return (file.OpenReadStream(), file.FileName);
        }

        return null;
    }
}
