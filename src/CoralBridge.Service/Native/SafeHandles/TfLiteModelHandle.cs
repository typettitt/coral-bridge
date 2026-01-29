using Microsoft.Win32.SafeHandles;

namespace CoralBridge.Native.SafeHandles;

/// <summary>
/// Safe handle for TfLiteModel pointer
/// </summary>
public sealed class TfLiteModelHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public TfLiteModelHandle() : base(ownsHandle: true) { }

    public TfLiteModelHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        TfLiteInterop.TfLiteModelDelete(handle);
        return true;
    }

    /// <summary>
    /// Creates a model from a file path.
    /// </summary>
    public static TfLiteModelHandle CreateFromFile(string modelPath)
    {
        var handle = TfLiteInterop.TfLiteModelCreateFromFile(modelPath);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to load model from: {modelPath}");
        }
        return new TfLiteModelHandle(handle);
    }
}
